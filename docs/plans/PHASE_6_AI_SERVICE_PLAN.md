# Phase 6 — AI Service Implementation Plan

> The standalone Python/FastAPI microservice centralizing all AI capabilities for the D'Jobsite iConnect platform.

## Overview

Phase 6 implements the AI Service that the monolith's Profiles, Recruitment, and Screening modules already have HTTP clients for. The service is a separate FastAPI deployment with its own PostgreSQL database (shared, tenant-ID filtered), Alembic migrations, and OpenAI integration.

**Scaffold status:** Project structure exists with empty packages. Only a `/health` endpoint is implemented in `app/main.py`.

**Monolith clients already built:** `AiResumeParserClient`, `AiCriteriaSuggesterClient`, `AiQuestionSuggesterClient`, `AiScoringClient`, `AiAnswerScoringClient`, `AiCandidateFeedbackClient`. The AI Service must implement the exact contracts these clients expect.

---

## Task Breakdown

### Step 1 — Core Infrastructure Setup

> Foundation layer: configuration, database connectivity, error handling, correlation ID, JWT auth.

#### 1.1 — Application Configuration (`app/core/config.py`)

- Pydantic Settings class with:
  - `database_url` — PostgreSQL async connection string (asyncpg)
  - `jwt_secret` — shared secret with monolith for token validation
  - `jwt_algorithm` — default `HS256`
  - `openai_api_key` — OpenAI API key
  - `openai_model` — default model (e.g., `gpt-4o`)
  - `cors_origins` — allowed origins list
  - `log_level` — default `INFO`
- Load from environment variables with `.env` fallback.

#### 1.2 — Database Engine & Session Factory (`app/infrastructure/db/`)

- `engine.py` — async SQLAlchemy engine creation with `asyncpg`
- `session.py` — `async_sessionmaker` factory, `get_db` dependency for FastAPI
- Configure connection pooling (`pool_size`, `max_overflow`)
- Ensure `ai_service` schema is created on startup

#### 1.3 — Error Handling (`app/core/errors.py`)

- `AppError` exception class matching the monolith's error envelope:
  ```python
  class AppError(Exception):
      def __init__(self, code: str, status_code: int, message: str, details: dict | None = None):
  ```
- `app_error_handler` — FastAPI exception handler returning:
  ```json
  {"code": "...", "message": "...", "request_id": "...", "details": {...}}
  ```
- Predefined sentinel errors:
  - `VALIDATION_ERROR` (400)
  - `UNAUTHORIZED` (401)
  - `INTERNAL_ERROR` (500)
  - `SERVICE_UNAVAILABLE` (503)
  - `AI_PROVIDER_ERROR` (502)

#### 1.4 — Correlation ID Middleware (`app/core/middleware.py`)

- Read `X-Correlation-ID` from request headers
- Generate UUID if absent
- Store on `request.state.correlation_id`
- Echo in response `X-Correlation-ID` header
- Attach to all log entries via `structlog` or `logging` context

#### 1.5 — JWT Authentication (`app/core/auth.py`)

- `decode_jwt(token: str) -> JwtClaims` — validate JWT using shared secret
- `JwtClaims` Pydantic model: `sub` (user_id), `tenant_id`, `role`, `email`
- `get_current_user` FastAPI dependency — extracts Bearer token, decodes, returns claims
- Return `UNAUTHORIZED` (401) for missing/invalid/expired tokens
- Extract `tenant_id` from claims for downstream use (logging, cost attribution)

#### 1.6 — Application Bootstrap (`app/main.py`)

- Register exception handlers (`AppError`, generic `Exception`)
- Add correlation ID middleware
- Mount API routers with `/api/v1/ai` prefix
- Configure CORS
- Keep existing `/health` endpoint (no auth required)
- Add startup event: verify DB connectivity, verify OpenAI API key

**Files to create/modify:**

| File                               | Action |
| ---------------------------------- | ------ |
| `app/core/config.py`               | Create |
| `app/core/errors.py`               | Create |
| `app/core/middleware.py`           | Create |
| `app/core/auth.py`                 | Create |
| `app/infrastructure/db/engine.py`  | Create |
| `app/infrastructure/db/session.py` | Create |
| `app/main.py`                      | Modify |

---

### Step 2 — SQLAlchemy Models & Alembic Migration

> Database layer: ORM models and initial migration for active tables.

#### 2.1 — SQLAlchemy Models (`app/core/models/`)

**`ai_api_log.py`** — `AiApiLog` model:

- All columns from `AI_SERVICE_DB_DESIGN.md` → `ai_api_logs` table
- Schema: `ai_service`
- `call_type` as `VARCHAR(30)` — values: `ResumeParsing`, `CriteriaGeneration`, `AssessmentQuestionGeneration`, `ScreeningEvaluation`, `AnswerScoring`, `FeedbackGeneration`
- `ai_provider` as `VARCHAR(20)` — values: `OpenAI`, `Anthropic`, `AzureOpenAI`
- JSONB columns: `request_summary`, `response_summary`
- All indexes from the DB design: `ix_api_logs_tenant_id`, `ix_api_logs_call_type`, `ix_api_logs_called_at`, `ix_api_logs_provider_model`, `ix_api_logs_is_success`
- `id` with `server_default=text("gen_random_uuid()")`
- `created_at` with `server_default=text("NOW()")`

**`parsed_resume_cache.py`** — `ParsedResumeCache` model:

- All columns from `AI_SERVICE_DB_DESIGN.md` → `parsed_resume_cache` table
- Schema: `ai_service`
- Unique constraint on `file_hash`
- Indexes: `ix_parsed_resume_cache_hash` (unique), `ix_parsed_resume_cache_expires`
- `expires_at` — default 30 days from creation

**`base.py`** — Declarative base with `ai_service` schema default

#### 2.2 — Alembic Setup & Initial Migration

- `alembic init migrations` (if not already configured)
- `alembic.ini` — async driver configuration for `asyncpg`
- `migrations/env.py` — import all models, configure `target_metadata`
- Generate migration: `alembic revision --autogenerate -m "create_ai_service_tables"`
- Tables created:
  - `ai_service.ai_api_logs` (with 5 indexes)
  - `ai_service.parsed_resume_cache` (with unique constraint + 2 indexes)

**Files to create/modify:**

| File                                                  | Action                 |
| ----------------------------------------------------- | ---------------------- |
| `app/core/models/base.py`                             | Create                 |
| `app/core/models/ai_api_log.py`                       | Create                 |
| `app/core/models/parsed_resume_cache.py`              | Create                 |
| `app/core/models/__init__.py`                         | Modify (export models) |
| `alembic.ini`                                         | Create                 |
| `migrations/env.py`                                   | Create                 |
| `migrations/versions/xxx_create_ai_service_tables.py` | Auto-generated         |

---

### Step 3 — AI Provider Abstraction

> OpenAI integration with a provider abstraction layer for future multi-provider support.

#### 3.1 — Provider Interface (`app/infrastructure/ai_providers/base.py`)

- `AiProvider` abstract base class / protocol:

  ```python
  class AiProvider(ABC):
      @abstractmethod
      async def complete(self, system_prompt: str, user_prompt: str, ...) -> AiCompletionResult:
          ...

      @property
      @abstractmethod
      def provider_name(self) -> str: ...

      @property
      @abstractmethod
      def model_name(self) -> str: ...
  ```

- `AiCompletionResult` dataclass:
  - `content: str` — the response text
  - `input_tokens: int | None`
  - `output_tokens: int | None`
  - `total_tokens: int | None`

#### 3.2 — OpenAI Provider (`app/infrastructure/ai_providers/openai_provider.py`)

- Implement `AiProvider` using `openai.AsyncOpenAI`
- Accept `api_key` and `model` from config
- Parse structured JSON responses (with `response_format={"type": "json_object"}`)
- Extract token usage from response
- Handle OpenAI-specific errors: `RateLimitError`, `APIError`, `APIConnectionError`
- Timeout handling per request

#### 3.3 — Provider Factory (`app/infrastructure/ai_providers/__init__.py`)

- `get_ai_provider(config: Settings) -> AiProvider` factory function
- Currently returns `OpenAiProvider`; extensible for `AnthropicProvider`, `AzureOpenAiProvider`

**Files to create:**

| File                                                 | Action |
| ---------------------------------------------------- | ------ |
| `app/infrastructure/ai_providers/base.py`            | Create |
| `app/infrastructure/ai_providers/openai_provider.py` | Create |
| `app/infrastructure/ai_providers/__init__.py`        | Modify |

---

### Step 4 — AI API Logging Service

> Cross-cutting concern: log every AI provider call to `ai_api_logs` for cost tracking and debugging.

#### 4.1 — Logging Service (`app/core/services/ai_logging_service.py`)

- `AiLoggingService` class:
  ```python
  async def log_call(
      self,
      tenant_id: UUID,
      call_type: str,          # ResumeParsing, CriteriaGeneration, etc.
      reference_id: UUID | None,
      provider: AiProvider,
      result: AiCompletionResult | None,
      latency_ms: int,
      http_status_code: int,
      is_success: bool,
      error_message: str | None = None,
      retry_count: int = 0,
      request_summary: dict | None = None,
      response_summary: dict | None = None,
  ) -> None:
  ```
- Compute `estimated_cost_usd` from token counts and model pricing lookup table
- Insert `AiApiLog` row via SQLAlchemy session
- **Never log full prompts or responses** — only metadata summaries (no PII)

#### 4.2 — Logging Decorator / Context Manager

- Convenience wrapper that:
  1. Records start time
  2. Calls the AI provider
  3. Records end time, computes latency
  4. Logs the call (success or failure)
  5. Returns the result (or re-raises on failure)
- Used by all service functions to ensure consistent logging

**Files to create:**

| File                                      | Action |
| ----------------------------------------- | ------ |
| `app/core/services/ai_logging_service.py` | Create |

---

### Step 5 — Pydantic Request/Response Schemas

> Define all endpoint schemas matching the monolith's client contracts exactly.

#### 5.1 — Resume Parsing Schemas (`app/api/schemas/resume.py`)

```python
class ResumeParseRequest(BaseModel):
    parsed_text: str                   # Plain text from the resume

class ExtractedSkill(BaseModel):
    name: str
    level: str | None = None           # e.g., "Advanced", "Intermediate"
    years: int | None = None

class Experience(BaseModel):
    title: str
    company: str
    start_date: str | None = None
    end_date: str | None = None
    description: str | None = None

class Education(BaseModel):
    degree: str
    institution: str
    start_date: str | None = None
    end_date: str | None = None
    field: str | None = None

class ResumeParseResponse(BaseModel):
    skills: list[ExtractedSkill] | None = None
    experience: list[Experience] | None = None
    education: list[Education] | None = None
    certifications: list[str] | None = None
    summary: str | None = None
```

#### 5.2 — Criteria Suggestion Schemas (`app/api/schemas/criteria.py`)

```python
class CriteriaSuggestRequest(BaseModel):
    job_title: str
    job_description: str

class CriteriaSuggestion(BaseModel):
    name: str
    category: str               # Skill|Experience|Certification|Education|Location|Custom
    evaluation_method: str      # ExactMatch|RangeMatch|SemanticSimilarity
    is_required: bool
    weight: Decimal              # 0.00–100.00
    configuration: str           # JSON string

class CriteriaSuggestResponse(BaseModel):
    suggestions: list[CriteriaSuggestion]
```

#### 5.3 — Assessment Question Schemas (`app/api/schemas/assessment.py`)

```python
class CriterionInput(BaseModel):
    id: UUID
    name: str
    category: str
    evaluation_method: str
    is_required: bool
    weight: Decimal
    configuration: str

class AssessmentSuggestRequest(BaseModel):
    job_description: str
    criteria: list[CriterionInput]

class QuestionSuggestion(BaseModel):
    question_text: str
    question_type: str           # FreeText|MultipleChoice|YesNo
    timing: str                  # AtApplication|AfterScreening
    is_required: bool
    weight: Decimal
    expected_answer: str | None = None
    options: str | None = None   # JSON array string for MultipleChoice

class AssessmentSuggestResponse(BaseModel):
    suggestions: list[QuestionSuggestion]
```

#### 5.4 — Screening Evaluation Schemas (`app/api/schemas/screening.py`)

```python
class ApplicantInput(BaseModel):
    profile_skills: str | None = None
    resume_parsed_text: str | None = None
    resume_extracted_skills: str | None = None
    ai_parsed_content: str | None = None

class ScreeningEvaluateRequest(BaseModel):
    criteria: list[CriterionInput]
    applicant: ApplicantInput

class CriterionScore(BaseModel):
    criterion_id: UUID
    criterion_name: str
    category: str
    weight: Decimal
    score: Decimal
    result: str                  # Pass|Fail|Required
    reasoning: str

class ScreeningEvaluateResponse(BaseModel):
    breakdown: list[CriterionScore]
    overall_score: Decimal
```

#### 5.5 — Answer Scoring Schemas (`app/api/schemas/answer_scoring.py`)

```python
class AnswerInput(BaseModel):
    question_id: UUID
    question_text: str
    response_text: str
    scoring_guidance: str | None = None
    key_topics: list[str] | None = None

class ScoreAnswersRequest(BaseModel):
    answers: list[AnswerInput]

class AnswerScoreResult(BaseModel):
    question_id: UUID
    score: Decimal               # 0–100
    result: str                  # Pass|Fail
    reasoning: str

class ScoreAnswersResponse(BaseModel):
    scores: list[AnswerScoreResult]
```

#### 5.6 — Candidate Feedback Schemas (`app/api/schemas/feedback.py`)

```python
class FeedbackRequest(BaseModel):
    criteria_breakdown: str       # JSON string of breakdown
    overall_score: Decimal
    transparency_level: str       # Full|Summary|None

class FeedbackResponse(BaseModel):
    feedback: str
```

**Files to create:**

| File                                | Action |
| ----------------------------------- | ------ |
| `app/api/schemas/__init__.py`       | Create |
| `app/api/schemas/resume.py`         | Create |
| `app/api/schemas/criteria.py`       | Create |
| `app/api/schemas/assessment.py`     | Create |
| `app/api/schemas/screening.py`      | Create |
| `app/api/schemas/answer_scoring.py` | Create |
| `app/api/schemas/feedback.py`       | Create |

---

### Step 6 — Business Logic Services

> Core AI logic: prompt engineering, response parsing, caching.

#### 6.1 — Resume Parsing Service (`app/core/services/resume_service.py`)

- Accept `ResumeParseRequest` + `tenant_id`
- Compute SHA-256 hash of `parsed_text`
- Check `parsed_resume_cache` by `file_hash`:
  - **Cache hit** (not expired) → return cached `parsed_result` immediately, skip AI call
  - **Cache miss or expired** → proceed with AI call
- Build system prompt: instruct AI to extract skills (with level/years), experience timeline, education, certifications, and summary
- Call AI provider via `AiProvider.complete()`
- Parse JSON response into `ResumeParseResponse`
- Store result in `parsed_resume_cache` with 30-day TTL
- Log call via `AiLoggingService` (call_type: `ResumeParsing`, reference_id: file hash as UUID or null)
- Return `ResumeParseResponse`

#### 6.2 — Criteria Generation Service (`app/core/services/criteria_service.py`)

- Accept `CriteriaSuggestRequest` + `tenant_id`
- Build system prompt: given job title and description, suggest evaluation criteria with appropriate categories, evaluation methods, weights, and configurations
- Instruct AI to use valid category values: `Skill`, `Experience`, `Certification`, `Education`, `Location`, `Custom`
- Instruct AI to use valid evaluation methods: `ExactMatch`, `RangeMatch`, `SemanticSimilarity`
- Call AI provider, parse response
- Log call (call_type: `CriteriaGeneration`)
- Return `list[CriteriaSuggestion]`

#### 6.3 — Assessment Question Service (`app/core/services/assessment_service.py`)

- Accept `AssessmentSuggestRequest` + `tenant_id`
- Build prompt: given job description + evaluation criteria, suggest AfterScreening questions
- Instruct AI to use valid question types: `FreeText`, `MultipleChoice`, `YesNo`
- Instruct AI to use valid timing: `AtApplication`, `AfterScreening`
- For `MultipleChoice`: generate `options` as JSON array string
- For all types: generate `expected_answer` as scoring rubric
- Call AI provider, parse response
- Log call (call_type: `AssessmentQuestionGeneration`)
- Return `list[QuestionSuggestion]`

#### 6.4 — AI Screening Service (`app/core/services/screening_service.py`)

- **`evaluate()`** — Accept `ScreeningEvaluateRequest` + `tenant_id`
  - Build prompt: given parsed resume/profile data + evaluation criteria, score each criterion
  - Instruct AI to return per-criterion scores (0–100), result (`Pass`/`Fail`/`Required`), and reasoning
  - Compute weighted `overall_score`
  - Log call (call_type: `ScreeningEvaluation`)
  - Return `ScreeningEvaluateResponse`

- **`score_answers()`** — Accept `ScoreAnswersRequest` + `tenant_id`
  - Build prompt: for each FreeText answer, evaluate quality against the question, scoring guidance, and key topics
  - Score each answer (0–100) with result (`Pass`/`Fail`) and reasoning
  - Log call (call_type: `AnswerScoring`, one log entry per batch)
  - Return `ScoreAnswersResponse`

- **`generate_feedback()`** — Accept `FeedbackRequest` + `tenant_id`
  - Build prompt: given criteria breakdown and overall score, generate candidate-facing feedback
  - Adapt detail level based on `transparency_level` (`Full` → detailed, `Summary` → high-level, `None` → generic)
  - Log call (call_type: `FeedbackGeneration`)
  - Return feedback string

**Files to create:**

| File                                      | Action |
| ----------------------------------------- | ------ |
| `app/core/services/resume_service.py`     | Create |
| `app/core/services/criteria_service.py`   | Create |
| `app/core/services/assessment_service.py` | Create |
| `app/core/services/screening_service.py`  | Create |
| `app/core/services/__init__.py`           | Modify |

---

### Step 7 — API Endpoints (FastAPI Routers)

> HTTP layer: routers implementing the exact contracts the monolith clients expect.

#### 7.1 — Resume Parsing Endpoint (`app/api/routes/resume.py`)

- `POST /api/v1/ai/resumes/parse`
- Auth: JWT required (extract `tenant_id` from claims)
- Request: `ResumeParseRequest`
- Response: `ResumeParseResponse` (200)
- Errors: 400 (validation), 401 (unauthorized), 502 (AI provider error), 500 (internal)
- Calls `ResumeService.parse()`

#### 7.2 — Criteria Suggestion Endpoint (`app/api/routes/criteria.py`)

- `POST /api/v1/ai/criteria/suggest`
- Auth: JWT required
- Request: `CriteriaSuggestRequest`
- Response: `list[CriteriaSuggestion]` (200)
- Calls `CriteriaService.suggest()`

#### 7.3 — Assessment Question Endpoint (`app/api/routes/assessment.py`)

- `POST /api/v1/ai/assessment/suggest`
- Auth: JWT required
- Request: `AssessmentSuggestRequest`
- Response: `list[QuestionSuggestion]` (200)
- Calls `AssessmentService.suggest()`

#### 7.4 — Screening Evaluation Endpoint (`app/api/routes/screening.py`)

- `POST /api/v1/ai/screening/evaluate`
- Auth: JWT required
- Request: `ScreeningEvaluateRequest`
- Response: `ScreeningEvaluateResponse` (200)
- Calls `ScreeningService.evaluate()`

#### 7.5 — Answer Scoring Endpoint (`app/api/routes/screening.py`)

- `POST /api/v1/ai/screening/score-answers`
- Auth: JWT required
- Request: `ScoreAnswersRequest`
- Response: `ScoreAnswersResponse` (200)
- Calls `ScreeningService.score_answers()`

#### 7.6 — Candidate Feedback Endpoint (`app/api/routes/screening.py`)

- `POST /api/v1/ai/screening/feedback`
- Auth: JWT required
- Request: `FeedbackRequest`
- Response: `FeedbackResponse` (200)
- Calls `ScreeningService.generate_feedback()`

#### 7.7 — Health Endpoint (existing)

- `GET /health` — no auth, returns `{"status": "healthy"}`
- Enhance with DB connectivity check (optional deep health check)

**Files to create/modify:**

| File                           | Action                    |
| ------------------------------ | ------------------------- |
| `app/api/routes/__init__.py`   | Create                    |
| `app/api/routes/resume.py`     | Create                    |
| `app/api/routes/criteria.py`   | Create                    |
| `app/api/routes/assessment.py` | Create                    |
| `app/api/routes/screening.py`  | Create                    |
| `app/api/__init__.py`          | Modify (register routers) |
| `app/main.py`                  | Modify (mount routers)    |

---

### Step 8 — Testing

> Complete test suite: unit tests for services, API endpoint tests, caching tests.

#### 8.1 — Test Infrastructure (`tests/conftest.py`)

- Shared fixtures:
  - `mock_ai_provider` — `AsyncMock` of `AiProvider` with configurable responses
  - `mock_db_session` — mock SQLAlchemy `AsyncSession`
  - `client` — FastAPI `TestClient` with dependency overrides (mock DB, mock AI provider)
  - `valid_jwt` — test JWT token signed with test secret
  - `jwt_claims` — `JwtClaims` fixture with `tenant_id`, `user_id`, `role`
- Import from `unittest.mock.AsyncMock` for async mocking
- Use `pytest-asyncio` with `asyncio_mode = "auto"`

#### 8.2 — Unit Tests: Services (`tests/test_services.py`)

Test naming: `test_{function}_{condition}_{expected}`

- **Resume Service:**
  - `test_parse_resume_valid_text_returns_structured_data` — AI returns skills/experience/education
  - `test_parse_resume_cached_result_returns_cache_hit` — no AI call when cache exists
  - `test_parse_resume_expired_cache_calls_ai_provider` — expired cache triggers fresh call
  - `test_parse_resume_stores_result_in_cache` — verifies cache write after AI call
  - `test_parse_resume_ai_failure_raises_app_error` — provider error → `AI_PROVIDER_ERROR`

- **Criteria Service:**
  - `test_suggest_criteria_valid_job_returns_suggestions` — returns criteria list with valid categories
  - `test_suggest_criteria_validates_category_values` — only valid category enums
  - `test_suggest_criteria_ai_failure_raises_app_error`

- **Assessment Service:**
  - `test_suggest_questions_valid_input_returns_questions` — returns questions with types/timing
  - `test_suggest_questions_includes_expected_answer` — scoring rubric present
  - `test_suggest_questions_multichoice_includes_options` — options JSON string present

- **Screening Service:**
  - `test_evaluate_valid_input_returns_per_criterion_scores` — scores with reasoning
  - `test_evaluate_computes_weighted_overall_score` — weighted average is correct
  - `test_score_answers_valid_input_returns_answer_scores` — per-answer scores with result
  - `test_generate_feedback_full_transparency_returns_detailed` — transparency level respected
  - `test_generate_feedback_summary_transparency_returns_concise`

- **AI Logging Service:**
  - `test_log_call_success_inserts_log_entry` — happy path logging
  - `test_log_call_computes_estimated_cost` — cost calculation from tokens
  - `test_log_call_failure_records_error_message` — error details captured

#### 8.3 — Unit Tests: Auth & Middleware (`tests/test_auth.py`)

- `test_decode_jwt_valid_token_returns_claims`
- `test_decode_jwt_expired_token_raises_unauthorized`
- `test_decode_jwt_invalid_signature_raises_unauthorized`
- `test_decode_jwt_missing_tenant_id_raises_unauthorized`
- `test_correlation_id_present_in_header_is_preserved`
- `test_correlation_id_absent_generates_uuid`

#### 8.4 — Unit Tests: Error Handling (`tests/test_errors.py`)

- `test_app_error_returns_correct_status_code`
- `test_app_error_returns_error_envelope_format`
- `test_app_error_includes_request_id`
- `test_app_error_with_details_includes_details`
- `test_unhandled_exception_returns_500_internal_error`

#### 8.5 — API Endpoint Tests (`tests/test_api.py`)

Using `httpx.AsyncClient` with FastAPI's `TestClient`:

- **Resume Parsing:**
  - `test_parse_resume_endpoint_returns_200` — valid request
  - `test_parse_resume_endpoint_missing_text_returns_400` — validation error
  - `test_parse_resume_endpoint_no_auth_returns_401` — missing token
  - `test_parse_resume_endpoint_invalid_jwt_returns_401`

- **Criteria Suggestion:**
  - `test_suggest_criteria_endpoint_returns_200`
  - `test_suggest_criteria_endpoint_missing_title_returns_400`

- **Assessment Suggestion:**
  - `test_suggest_assessment_endpoint_returns_200`
  - `test_suggest_assessment_endpoint_empty_criteria_returns_400`

- **Screening Evaluation:**
  - `test_evaluate_endpoint_returns_200`
  - `test_evaluate_endpoint_missing_criteria_returns_400`

- **Answer Scoring:**
  - `test_score_answers_endpoint_returns_200`
  - `test_score_answers_endpoint_empty_answers_returns_400`

- **Candidate Feedback:**
  - `test_feedback_endpoint_returns_200`
  - `test_feedback_endpoint_invalid_transparency_level_returns_400`

#### 8.6 — Caching Tests (`tests/test_caching.py`)

- `test_resume_cache_hit_skips_ai_call` — verify no AI provider invocation
- `test_resume_cache_miss_triggers_ai_call` — verify AI provider called
- `test_resume_cache_expired_entry_triggers_ai_call`
- `test_resume_cache_stores_with_30_day_ttl`
- `test_resume_cache_same_hash_different_tenant_returns_cached`

#### 8.7 — Schema Validation Tests (`tests/test_schemas.py`)

- Validate all Pydantic schemas serialize/deserialize with `snake_case` correctly
- Verify enum values match PascalCase conventions
- Verify optional fields default to `None`
- Test response model serialization matches what monolith clients expect

**Files to create:**

| File                     | Action |
| ------------------------ | ------ |
| `tests/conftest.py`      | Create |
| `tests/test_services.py` | Create |
| `tests/test_auth.py`     | Create |
| `tests/test_errors.py`   | Create |
| `tests/test_api.py`      | Create |
| `tests/test_caching.py`  | Create |
| `tests/test_schemas.py`  | Create |

---

### Step 9 — Documentation

> API reference docs and update project-level documentation.

#### 9.1 — AI Service API Reference (`docs/api-reference/ai-service.md`)

Document all 6 endpoints with:

- Method, path, description
- Authentication requirements
- Request body schema (with field descriptions and constraints)
- Response body schema (with field descriptions)
- Error codes (with HTTP status codes and when they occur)
- Example request/response JSON for each endpoint
- Rate limiting notes (future)
- Caching behavior (resume parsing only)

#### 9.2 — Update `docs/api-reference/README.md`

- Add AI Service section linking to `ai-service.md`

#### 9.3 — Update `docs/TODO.md`

- Move Phase 6 items from "deferred" to "completed" (or "in progress")
- Add any new stubs or deferred items discovered during implementation

#### 9.4 — AI Service README (`ai-service/README.md`)

Update the existing README with:

- Prerequisites (Python 3.12+, PostgreSQL, OpenAI API key)
- Environment variables reference
- Setup instructions (venv, install, DB migration, run)
- Test instructions (`pytest`)
- Available endpoints summary
- Architecture overview (config → middleware → routes → services → AI provider → DB)

#### 9.5 — Update `docs/DEVELOPMENT_PLAN.md`

- Mark Phase 6 tasks as completed
- Update "Current State Summary" with AI Service implementation status

**Files to create/modify:**

| File                               | Action |
| ---------------------------------- | ------ |
| `docs/api-reference/ai-service.md` | Create |
| `docs/api-reference/README.md`     | Modify |
| `docs/TODO.md`                     | Modify |
| `ai-service/README.md`             | Modify |
| `docs/DEVELOPMENT_PLAN.md`         | Modify |

---

## Implementation Order

The tasks above are grouped by concern. The recommended implementation sequence respects dependency order:

```
Step 1  → Core Infrastructure (config, DB, errors, auth, middleware)
Step 2  → SQLAlchemy Models + Alembic Migration
Step 3  → AI Provider Abstraction (OpenAI)
Step 4  → AI API Logging Service
Step 5  → Pydantic Schemas (all request/response types)
Step 6  → Business Logic Services (resume, criteria, assessment, screening)
Step 7  → API Endpoints (FastAPI routers)
Step 8  → Testing (unit + API + caching + schema tests)
Step 9  → Documentation (API reference, README updates)
```

Steps 1–4 are foundation. Steps 5–6 can overlap. Steps 7 depends on 5–6. Steps 8–9 run last.

---

## Conventions Checklist

These conventions must be followed throughout implementation (sourced from project docs):

### Python / FastAPI

- [ ] **Pydantic v2** for all request/response schemas and event payloads
- [ ] **SQLAlchemy 2.0** async style with `asyncpg`
- [ ] **`snake_case`** for all JSON fields (matching monolith's global config)
- [ ] **`PascalCase`** for status/enum values (matching CHECK constraints)
- [ ] **`tenant_id`** as leading column in all composite indexes
- [ ] **Same error envelope** as monolith: `{"code": "...", "message": "...", "request_id": "..."}`
- [ ] All endpoints require **JWT Bearer auth** (except `/health`)
- [ ] **`X-Correlation-ID`** read from inbound requests; generate UUID if absent
- [ ] **`AppError`** exception class with FastAPI exception handler
- [ ] Schema: `ai_service.*` for all tables
- [ ] No FKs to monolith — `tenant_id` and `reference_id` are correlation keys only
- [ ] **Never log full prompts or AI responses** — only metadata summaries (no PII)

### Testing (Python)

- [ ] `pytest` + `pytest-asyncio` with `asyncio_mode = "auto"`
- [ ] Test naming: `test_{function}_{condition}_{expected}`
- [ ] `unittest.mock.AsyncMock` for async dependencies
- [ ] Standard `assert` statements — no assertion library needed
- [ ] Fixtures for dependency injection of mocks
- [ ] Separate test files by concern: `test_services.py`, `test_api.py`, `test_auth.py`, `test_errors.py`, `test_caching.py`, `test_schemas.py`

### API Conventions

- [ ] Route prefix: `/api/v1/ai/...`
- [ ] `Content-Type: application/json` for all responses
- [ ] Error responses use canonical error envelope
- [ ] Null fields omitted from responses (Pydantic's `model_config = ConfigDict(exclude_none=True)`)
- [ ] HTTP status codes: 200 (success), 400 (validation), 401 (unauthorized), 502 (AI provider), 500 (internal)

### Database Conventions

- [ ] PostgreSQL UUID PKs with `gen_random_uuid()`
- [ ] `TIMESTAMPTZ` for all timestamp columns with `NOW()` default
- [ ] `VARCHAR` with CHECK constraints for enum columns (PascalCase values)
- [ ] JSONB for flexible structured data
- [ ] Index naming: `ix_{table}_{column}`
- [ ] Unique constraint naming: `uq_{table}_{column}`

---

## File Inventory

### New Files (29)

| #   | Path                                                 | Purpose                                    |
| --- | ---------------------------------------------------- | ------------------------------------------ |
| 1   | `app/core/config.py`                                 | Pydantic Settings                          |
| 2   | `app/core/errors.py`                                 | AppError + exception handler               |
| 3   | `app/core/middleware.py`                             | Correlation ID middleware                  |
| 4   | `app/core/auth.py`                                   | JWT decode + FastAPI dependency            |
| 5   | `app/core/models/base.py`                            | SQLAlchemy declarative base                |
| 6   | `app/core/models/ai_api_log.py`                      | AiApiLog ORM model                         |
| 7   | `app/core/models/parsed_resume_cache.py`             | ParsedResumeCache ORM model                |
| 8   | `app/infrastructure/db/engine.py`                    | Async engine factory                       |
| 9   | `app/infrastructure/db/session.py`                   | Async session factory + `get_db`           |
| 10  | `app/infrastructure/ai_providers/base.py`            | AiProvider ABC                             |
| 11  | `app/infrastructure/ai_providers/openai_provider.py` | OpenAI implementation                      |
| 12  | `app/core/services/ai_logging_service.py`            | AI call audit logging                      |
| 13  | `app/core/services/resume_service.py`                | Resume parsing + caching                   |
| 14  | `app/core/services/criteria_service.py`              | Criteria suggestion                        |
| 15  | `app/core/services/assessment_service.py`            | Assessment question suggestion             |
| 16  | `app/core/services/screening_service.py`             | Screening eval + answer scoring + feedback |
| 17  | `app/api/schemas/__init__.py`                        | Schema package                             |
| 18  | `app/api/schemas/resume.py`                          | Resume parse DTOs                          |
| 19  | `app/api/schemas/criteria.py`                        | Criteria suggestion DTOs                   |
| 20  | `app/api/schemas/assessment.py`                      | Assessment question DTOs                   |
| 21  | `app/api/schemas/screening.py`                       | Screening evaluation DTOs                  |
| 22  | `app/api/schemas/answer_scoring.py`                  | Answer scoring DTOs                        |
| 23  | `app/api/schemas/feedback.py`                        | Candidate feedback DTOs                    |
| 24  | `app/api/routes/__init__.py`                         | Routes package                             |
| 25  | `app/api/routes/resume.py`                           | Resume endpoint                            |
| 26  | `app/api/routes/criteria.py`                         | Criteria endpoint                          |
| 27  | `app/api/routes/assessment.py`                       | Assessment endpoint                        |
| 28  | `app/api/routes/screening.py`                        | Screening endpoints (3)                    |
| 29  | `docs/api-reference/ai-service.md`                   | API reference documentation                |

### Modified Files (8)

| #   | Path                                          | Change                                       |
| --- | --------------------------------------------- | -------------------------------------------- |
| 1   | `app/main.py`                                 | Register routers, middleware, error handlers |
| 2   | `app/core/models/__init__.py`                 | Export models                                |
| 3   | `app/core/services/__init__.py`               | Export services                              |
| 4   | `app/infrastructure/ai_providers/__init__.py` | Provider factory                             |
| 5   | `app/api/__init__.py`                         | Register routers                             |
| 6   | `ai-service/README.md`                        | Updated setup/usage docs                     |
| 7   | `docs/api-reference/README.md`                | Add AI Service link                          |
| 8   | `docs/TODO.md`                                | Update Phase 6 status                        |

### Auto-Generated Files (2)

| #   | Path                                                  | Source                            |
| --- | ----------------------------------------------------- | --------------------------------- |
| 1   | `alembic.ini`                                         | `alembic init`                    |
| 2   | `migrations/versions/xxx_create_ai_service_tables.py` | `alembic revision --autogenerate` |

### Test Files (7)

| #   | Path                     | Coverage                           |
| --- | ------------------------ | ---------------------------------- |
| 1   | `tests/conftest.py`      | Shared fixtures                    |
| 2   | `tests/test_services.py` | All 4 service classes (~18 tests)  |
| 3   | `tests/test_auth.py`     | JWT decode + middleware (~6 tests) |
| 4   | `tests/test_errors.py`   | Error handling (~5 tests)          |
| 5   | `tests/test_api.py`      | All 6 endpoints (~14 tests)        |
| 6   | `tests/test_caching.py`  | Resume cache behavior (~5 tests)   |
| 7   | `tests/test_schemas.py`  | Pydantic serialization (~6 tests)  |

**Total estimated tests: ~54**

---

## Exit Criteria

- [ ] AI Service provides all 6 HTTP endpoints matching the monolith's client contracts
- [ ] Resume parse results are cached by file hash (SHA-256) with 30-day TTL
- [ ] All AI provider calls are logged to `ai_api_logs` with token usage, cost, and latency
- [ ] JWT validation works with the monolith's shared secret
- [ ] Correlation ID flows through all requests and log entries
- [ ] Error responses use the canonical error envelope format
- [ ] All tests pass (`pytest` — ~54 tests)
- [ ] API reference documentation is complete
- [ ] Alembic migration creates `ai_service.ai_api_logs` and `ai_service.parsed_resume_cache` tables
- [ ] No PII or full prompts are stored in logs
