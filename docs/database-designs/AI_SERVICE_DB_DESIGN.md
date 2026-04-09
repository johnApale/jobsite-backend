# AI Service — Database Design

Standalone microservice database. This is **not** part of the modular monolith's tenant DB — it's a separate PostgreSQL database owned by the AI Service. Unlike the monolith (which uses database-per-tenant), this service uses a **single shared database with tenant ID filtering** because provisioning a separate database per tenant would be overkill for lightweight, mostly-transient data.

The AI Service centralizes all AI capabilities for the platform. It communicates with the monolith via **HTTP for synchronous operations** (criteria suggestion, assessment question suggestion) and via **message broker for asynchronous operations** (resume parsing, AI screening, answer scoring, candidate feedback).

## Active Capabilities

1. **Resume Parsing** — AI-powered structured data extraction from resumes (skills with levels/years, experience timeline, education, certifications). Called by the Profiles module on upload.
2. **Criteria Generation** — AI-assisted evaluation criteria suggestions from job descriptions. Called by the Recruitment module during job setup.
3. **Assessment Question Generation** — AI-suggested screening questions for the AfterScreening phase. Feature-flagged (system gate + tenant opt-in). Called by the Recruitment module during job setup.
4. **AI Screening** — Alternative scoring engine for candidate evaluation. Feature-flagged (system gate + tenant opt-in). Runs alongside deterministic scoring when enabled. Called by the Screening module during evaluation.

## Deferred Capability

5. **AI Interview** — Real-time AI-conducted interview sessions with question generation, media handling, transcription, response scoring, and comprehensive evaluation. Very low priority future item. See [AI Interview Tables (Deferred)](#ai-interview-tables-deferred) section below.

---

## Active Tables

### ai_api_logs

Audit trail for every AI provider API call made by the service across all capability domains. Each call — resume parsing, criteria generation, screening evaluation, answer scoring, etc. — gets its own log entry. Used for cost tracking, debugging, compliance auditing, and performance monitoring.

| Column             | Type          | Constraints         | Description                                                                                                                                                                                                                                                   |
| ------------------ | ------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| id                 | uuid          | PK                  |                                                                                                                                                                                                                                                               |
| tenant_id          | uuid          | NOT NULL            | The tenant this call was made on behalf of. For cost attribution                                                                                                                                                                                              |
| call_type          | varchar(30)   | NOT NULL            | Enum: `ResumeParsing`, `CriteriaGeneration`, `AssessmentQuestionGeneration`, `ScreeningEvaluation`, `AnswerScoring`, `FeedbackGeneration`, `QuestionGeneration`, `ResponseScoring`, `EvaluationGeneration`, `Transcription`. What capability this call serves |
| reference_id       | uuid          | nullable            | The specific entity this call relates to (e.g., application_id for screening, resume file hash for parsing). NULL for calls without a specific entity reference                                                                                               |
| ai_provider        | varchar(20)   | NOT NULL            | Enum: `OpenAI`, `Anthropic`, `AzureOpenAI`. Which provider was called                                                                                                                                                                                         |
| ai_model           | varchar(50)   | NOT NULL            | Specific model used (e.g., `gpt-4o`, `claude-sonnet-4-20250514`)                                                                                                                                                                                              |
| input_tokens       | integer       | nullable            | Number of tokens in the request prompt. NULL if the provider doesn't report this                                                                                                                                                                              |
| output_tokens      | integer       | nullable            | Number of tokens in the response. NULL if the provider doesn't report this                                                                                                                                                                                    |
| total_tokens       | integer       | nullable            | Total tokens consumed (input + output)                                                                                                                                                                                                                        |
| estimated_cost_usd | decimal(10,6) | nullable            | Estimated cost in USD based on token usage and model pricing. Computed at call time                                                                                                                                                                           |
| latency_ms         | integer       | NOT NULL            | Round-trip time for the API call in milliseconds                                                                                                                                                                                                              |
| http_status_code   | integer       | NOT NULL            | HTTP status code returned by the provider                                                                                                                                                                                                                     |
| is_success         | boolean       | NOT NULL            | Whether the call succeeded (2xx response and valid output)                                                                                                                                                                                                    |
| error_message      | varchar(1000) | nullable            | Error details if the call failed. NULL on success                                                                                                                                                                                                             |
| retry_count        | integer       | NOT NULL, DEFAULT 0 | How many retries were attempted before this result                                                                                                                                                                                                            |
| request_summary    | jsonb         | nullable            | Summarized input — NOT the full prompt (which may contain PII). Metadata like: criteria count, resume length, question count requested                                                                                                                        |
| response_summary   | jsonb         | nullable            | Summarized output — NOT the full response. Metadata like: criteria suggested count, score assigned, questions generated count                                                                                                                                 |
| called_at          | timestamp     | NOT NULL            | When the API call was initiated                                                                                                                                                                                                                               |
| created_at         | timestamp     | NOT NULL            |                                                                                                                                                                                                                                                               |

**Indexes:**

| Name                       | Columns               | Type       | Purpose                                                 |
| -------------------------- | --------------------- | ---------- | ------------------------------------------------------- |
| ix_api_logs_tenant_id      | tenant_id             | Non-unique | Cost attribution per tenant                             |
| ix_api_logs_call_type      | call_type             | Non-unique | Filter by operation type for cost analysis              |
| ix_api_logs_called_at      | called_at             | Non-unique | Time-range queries for usage dashboards, cost reports   |
| ix_api_logs_provider_model | ai_provider, ai_model | Non-unique | Cost and performance comparison across providers/models |
| ix_api_logs_is_success     | is_success            | Non-unique | Find failed calls for debugging and retry analysis      |

---

### parsed_resume_cache

Caches AI-parsed resume results keyed by file hash. Avoids re-parsing identical files uploaded by different tenants or re-uploaded by the same applicant. The Profiles module checks this cache before requesting a fresh parse.

| Column        | Type        | Constraints | Description                                                                                                                                  |
| ------------- | ----------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| id            | uuid        | PK          |                                                                                                                                              |
| file_hash     | varchar(64) | NOT NULL    | SHA-256 hash of the resume file. The cache key                                                                                               |
| parsed_result | jsonb       | NOT NULL    | The structured extraction result (skills, experience, education, certifications). Same format stored in `profiles.resumes.ai_parsed_content` |
| ai_provider   | varchar(20) | NOT NULL    | Which provider produced this result                                                                                                          |
| ai_model      | varchar(50) | NOT NULL    | Which model produced this result                                                                                                             |
| expires_at    | timestamp   | NOT NULL    | Cache TTL. Entries older than this are eligible for cleanup. Default: 30 days from creation                                                  |
| created_at    | timestamp   | NOT NULL    |                                                                                                                                              |

**Constraints:**

| Name                        | Columns   | Type   | Purpose                         |
| --------------------------- | --------- | ------ | ------------------------------- |
| uq_parsed_resume_cache_hash | file_hash | Unique | One cached result per file hash |

**Indexes:**

| Name                           | Columns    | Type       | Purpose                               |
| ------------------------------ | ---------- | ---------- | ------------------------------------- |
| ix_parsed_resume_cache_hash    | file_hash  | Unique     | Fast lookup by file hash              |
| ix_parsed_resume_cache_expires | expires_at | Non-unique | Cleanup job to remove expired entries |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS ai_service;
```

All tables live under the `ai_service` schema in the AI Service's own database.

---

## Relationships

```
ai_api_logs : standalone (no FKs — tenant_id and reference_id are correlation keys)
parsed_resume_cache : standalone (no FKs — keyed by file content hash)
```

**No FKs to the monolith's database.** `tenant_id` and `reference_id` are stored as plain UUIDs — not foreign keys. This service cannot and should not reference the monolith's tables. The IDs are correlation keys used to route data back to the correct tenant and entity.

---

## Tenant Isolation

This is the one database in the system that uses **tenant ID filtering** instead of database-per-tenant. Every query on `ai_api_logs` must include `tenant_id` in the WHERE clause. The `parsed_resume_cache` table is tenant-agnostic (keyed by file hash, not tenant).

**Why not database-per-tenant here?** The AI Service stores lightweight, mostly-transient data: API call logs for cost tracking and cached parse results for deduplication. Provisioning and managing a separate database per tenant for this data would add operational complexity for no meaningful benefit. A single database with tenant filtering is simpler and sufficient at the expected scale.

---

## AI Service API Endpoints

The following endpoints are documented here for context. Implementation details are in the AI Service's own codebase.

| Method | Path                                 | Description                                                                        | Called By                       |
| ------ | ------------------------------------ | ---------------------------------------------------------------------------------- | ------------------------------- |
| `POST` | `/api/v1/ai/resumes/parse`           | AI-powered structured resume extraction                                            | Profiles module (on upload)     |
| `POST` | `/api/v1/ai/criteria/suggest`        | Suggest evaluation criteria from job description                                   | Recruitment module (job setup)  |
| `POST` | `/api/v1/ai/assessment/suggest`      | Suggest AfterScreening questions. **Feature-flagged**                              | Recruitment module (job setup)  |
| `POST` | `/api/v1/ai/screening/evaluate`      | AI analysis scoring for candidate evaluation. **Feature-flagged on consumer side** | Screening module (evaluation)   |
| `POST` | `/api/v1/ai/screening/score-answers` | Score FreeText question answers                                                    | Screening module (evaluation)   |
| `POST` | `/api/v1/ai/screening/feedback`      | Generate candidate-facing feedback                                                 | Screening module (transparency) |
| `GET`  | `/health`                            | Health check                                                                       | Infrastructure                  |

---

## AI API Audit & Cost Tracking

The `ai_api_logs` table provides a complete audit trail for every AI provider call across all capabilities. Example call patterns:

```
Resume parse (1 resume):
  1× ResumeParsing          — extract structured data from resume text
  ─────────────────────────
  1 API call logged

Criteria suggestion (1 job):
  1× CriteriaGeneration     — analyze job description, suggest criteria
  ─────────────────────────
  1 API call logged

AI screening evaluation (1 candidate):
  1× ScreeningEvaluation    — score all criteria against resume/profile
  ─────────────────────────
  1 API call logged (or N if scored per-criterion)

FreeText answer scoring (3 questions):
  3× AnswerScoring           — score each FreeText response
  ─────────────────────────
  3 API calls logged
```

**What's tracked per call:**

- Token usage (input, output, total) for cost attribution
- Estimated cost in USD computed at call time using current model pricing
- Latency for performance monitoring and SLA tracking
- Success/failure status with error messages for debugging
- Retry count to detect flaky provider behavior
- Request/response summaries (metadata only — no PII or full prompts)

**What this enables:**

- **Cost attribution per tenant** — query by `tenant_id` to see AI usage per tenant. Essential for usage-based billing or subscription tier enforcement
- **Cost attribution per capability** — query by `call_type` to see which capabilities drive the most cost
- **Provider comparison** — compare latency, error rates, and cost across providers/models if running A/B tests
- **Failure analysis** — find patterns in failed calls (time of day, specific models, specific call types)
- **Usage dashboards** — total tokens consumed, cost per evaluation, cost trends over time

**What's NOT stored:**

- Full prompts (may contain candidate PII, job details, or proprietary scoring logic)
- Full AI responses (stored on the relevant entity in the monolith — criteria, scores, evaluations)
- Raw HTTP request/response bodies

---

## AI Interview Tables (Deferred)

> **⚠️ DEFERRED — Very Low Priority**
>
> The tables below are designed but not yet implemented. They will be created when the AI Interview capability is built. The AI Service scaffold supports this future work — the schema, provider abstraction, and broker integration are designed to accommodate it.

The AI Interview capability will handle real-time AI-conducted interview sessions: generating interview questions tailored to job requirements, presenting them to candidates (text, voice, or video), transcribing media responses, scoring each response individually, and producing a comprehensive evaluation.

**Inbound (future):** `CandidateReadyForInterviewEvent` from Screening (via message broker)
**Outbound (future):** `InterviewCompletedEvent` to Matching (via message broker)

### interview_sessions (deferred)

One session per candidate per application. Created when `CandidateReadyForInterviewEvent` is received.

| Column             | Type         | Constraints         | Description                                                                                         |
| ------------------ | ------------ | ------------------- | --------------------------------------------------------------------------------------------------- |
| id                 | uuid         | PK                  |                                                                                                     |
| tenant_id          | uuid         | NOT NULL            | Tenant isolation                                                                                    |
| application_id     | uuid         | NOT NULL            | Correlation key to monolith's application                                                           |
| job_posting_id     | uuid         | NOT NULL            | Denormalized for context in question generation                                                     |
| applicant_id       | uuid         | NOT NULL            | For JWT validation and session ownership                                                            |
| status             | varchar(20)  | NOT NULL            | Enum: `Pending`, `QuestionGeneration`, `InProgress`, `Processing`, `Completed`, `Expired`, `Failed` |
| job_title          | varchar(200) | NOT NULL            | Denormalized for question generation prompts                                                        |
| job_requirements   | jsonb        | NOT NULL            | Denormalized evaluation criteria for AI question generation                                         |
| total_questions    | integer      | nullable            | Number of questions generated                                                                       |
| questions_answered | integer      | NOT NULL, DEFAULT 0 | Progress tracking                                                                                   |
| expires_at         | timestamp    | NOT NULL            | Deadline for completion                                                                             |
| started_at         | timestamp    | nullable            | When the candidate began                                                                            |
| completed_at       | timestamp    | nullable            | When the candidate finished                                                                         |
| created_at         | timestamp    | NOT NULL            |                                                                                                     |
| updated_at         | timestamp    | NOT NULL            |                                                                                                     |

### interview_questions (deferred)

Individual questions generated by the AI for a session.

| Column          | Type        | Constraints                          | Description                                                                   |
| --------------- | ----------- | ------------------------------------ | ----------------------------------------------------------------------------- |
| id              | uuid        | PK                                   |                                                                               |
| session_id      | uuid        | NOT NULL, FK → interview_sessions.id |                                                                               |
| question_number | integer     | NOT NULL                             | Ordering within session                                                       |
| category        | varchar(30) | NOT NULL                             | Enum: `Technical`, `Behavioral`, `Situational`, `Experience`, `Communication` |
| difficulty      | varchar(20) | NOT NULL                             | Enum: `Easy`, `Medium`, `Hard`                                                |
| question_text   | text        | NOT NULL                             | The question                                                                  |
| expected_topics | jsonb       | nullable                             | Key topics for scoring                                                        |
| target_skills   | jsonb       | nullable                             | Which skills this evaluates                                                   |
| created_at      | timestamp   | NOT NULL                             |                                                                               |

### interview_responses (deferred)

Candidate answers to interview questions.

| Column                    | Type          | Constraints                           | Description                                           |
| ------------------------- | ------------- | ------------------------------------- | ----------------------------------------------------- |
| id                        | uuid          | PK                                    |                                                       |
| question_id               | uuid          | NOT NULL, FK → interview_questions.id |                                                       |
| session_id                | uuid          | NOT NULL, FK → interview_sessions.id  | Denormalized                                          |
| response_type             | varchar(20)   | NOT NULL                              | Enum: `Text`, `Voice`, `Video`, `Skipped`, `TimedOut` |
| response_text             | text          | nullable                              | Text or transcript                                    |
| media_url                 | varchar(2048) | nullable                              | Audio/video URL                                       |
| media_type                | varchar(10)   | nullable                              | Enum: `Audio`, `Video`                                |
| media_duration_seconds    | integer       | nullable                              |                                                       |
| transcription_status      | varchar(20)   | nullable                              | Enum: `Pending`, `Completed`, `Failed`                |
| transcription_provider    | varchar(30)   | nullable                              |                                                       |
| transcribed_at            | timestamp     | nullable                              |                                                       |
| response_duration_seconds | integer       | nullable                              |                                                       |
| submitted_at              | timestamp     | NOT NULL                              |                                                       |
| created_at                | timestamp     | NOT NULL                              |                                                       |

### response_evaluations (deferred)

AI's assessment of an individual response. One-to-one with `interview_responses` using shared PK.

| Column          | Type         | Constraints                     | Description                          |
| --------------- | ------------ | ------------------------------- | ------------------------------------ |
| response_id     | uuid         | PK, FK → interview_responses.id | Shared key                           |
| score           | decimal(5,2) | NOT NULL                        | 0.00–100.00                          |
| score_reasoning | text         | NOT NULL                        | AI's explanation                     |
| topics_covered  | jsonb        | nullable                        | Which expected topics were addressed |
| evaluated_at    | timestamp    | NOT NULL                        |                                      |
| created_at      | timestamp    | NOT NULL                        |                                      |

### interview_evaluations (deferred)

Comprehensive evaluation after all responses are scored. One-to-one with `interview_sessions` using shared PK. This is the data sent back to the monolith via `InterviewCompletedEvent`.

| Column              | Type         | Constraints                    | Description                                                    |
| ------------------- | ------------ | ------------------------------ | -------------------------------------------------------------- |
| session_id          | uuid         | PK, FK → interview_sessions.id | Shared key                                                     |
| overall_score       | decimal(5,2) | NOT NULL                       | Final interview score (0.00–100.00)                            |
| technical_score     | decimal(5,2) | NOT NULL                       | Technical knowledge (0.00–100.00)                              |
| communication_score | decimal(5,2) | NOT NULL                       | Clarity and articulation (0.00–100.00)                         |
| experience_score    | decimal(5,2) | NOT NULL                       | Experience depth/relevance (0.00–100.00)                       |
| behavioral_score    | decimal(5,2) | nullable                       | Teamwork, leadership (0.00–100.00)                             |
| recommendation      | varchar(20)  | NOT NULL                       | Enum: `StrongAdvance`, `Advance`, `Borderline`, `DoNotAdvance` |
| strengths           | jsonb        | NOT NULL                       | Key strengths identified                                       |
| concerns            | jsonb        | NOT NULL                       | Concerns or weaknesses                                         |
| summary             | text         | NOT NULL                       | Narrative summary                                              |
| score_breakdown     | jsonb        | NOT NULL                       | Per-category breakdown with weights                            |
| evaluated_at        | timestamp    | NOT NULL                       |                                                                |
| created_at          | timestamp    | NOT NULL                       |                                                                |

### Deferred Relationships

```
interview_sessions     ||--o{ interview_questions    : "has many"
interview_sessions     ||--o{ interview_responses    : "has many (denormalized)"
interview_questions    ||--o| interview_responses    : "has (optional, one-to-one)"
interview_responses    ||--o| response_evaluations   : "has (optional, one-to-one)"
interview_sessions     ||--o| interview_evaluations  : "has (optional, one-to-one)"
interview_sessions     ||--o{ ai_api_logs            : "has many (via reference_id)"
```

No FKs to the monolith. `tenant_id`, `application_id`, `job_posting_id`, and `applicant_id` are correlation keys.

### Deferred Interview Session Lifecycle

```
Pending → QuestionGeneration → InProgress → Processing → Completed
                                           → Expired
       → Failed (at any stage)
```

---

## Design Decisions

**Shared database with tenant ID filtering.** The AI Service stores API logs and cache data — lightweight, mostly-transient records. Database-per-tenant would add operational overhead for data that doesn't require the same isolation guarantees as user profiles or application records.

**`ai_api_logs` as a shared table across all capabilities.** All AI provider calls — regardless of capability domain — are logged in the same table with a `call_type` discriminator. This gives a single view of AI costs and usage across the entire platform. Per-capability log tables would fragment cost analysis.

**`parsed_resume_cache` is tenant-agnostic.** Resumes are cached by file hash, not by tenant. If two tenants upload the same file (unlikely but possible), the cached result is reused. This is safe because the parse result is purely structural — no tenant-specific data is injected.

**No FKs to the monolith.** This service is independently deployable and can't reference the monolith's database. All monolith entity IDs are stored as plain UUIDs for correlation.

**Interview tables deferred, not deleted.** The schema design is preserved for future implementation. When AI Interview is built, these tables get an Alembic migration and the broker consumer/publisher are activated. No redesign needed — just implementation.

**HTTP for sync, broker for async.** Active capabilities (parsing, criteria, screening) use direct HTTP calls from the monolith. The AI Interview (future) uses the message broker because interview sessions are long-running and async. Both patterns coexist in the service.

**Provider abstraction.** The `ai_provider` and `ai_model` columns on both `ai_api_logs` and `parsed_resume_cache` track which provider/model produced each result. This supports future multi-provider setups and A/B testing without changing the schema.
