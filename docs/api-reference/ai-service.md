# AI Service API Reference

The AI Service is a standalone **Python/FastAPI** microservice that provides AI-powered capabilities for the D'Jobsite iConnect platform.

## Base URL

```
http://localhost:8000
```

## Authentication

All endpoints (except `/health`) require a **JWT Bearer** token in the `Authorization` header.

```
Authorization: Bearer <access_token>
```

The service validates tokens using a shared HS256 secret with the .NET monolith. Required claims: `sub`, `tenant_id`, `role`, `email`.

## Headers

| Header             | Required | Description                                                                |
| ------------------ | -------- | -------------------------------------------------------------------------- |
| `Authorization`    | Yes      | `Bearer <JWT>` — required on all endpoints except `/health`                |
| `X-Correlation-ID` | No       | Propagated through logs and echoed in responses. Auto-generated if absent. |
| `Content-Type`     | Yes      | `application/json`                                                         |

## Error Envelope

All errors follow the canonical envelope format:

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "request_id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "details": { ... }
}
```

Error codes: `VALIDATION_ERROR` (400), `UNAUTHORIZED` (401), `FORBIDDEN` (403), `INTERNAL_ERROR` (500), `AI_PROVIDER_ERROR` (502), `SERVICE_UNAVAILABLE` (503).

---

## Endpoints

### Health Check

```
GET /health
```

No authentication required.

**Response** `200 OK`

```json
{ "status": "healthy" }
```

---

## Message Broker Operations (RabbitMQ)

The following operations are handled asynchronously via RabbitMQ. The AI Service consumes request events, processes them, and publishes response events back to the monolith.

### Resume Parsing

| Direction | Event                  | Key Fields                                    |
| --------- | ---------------------- | --------------------------------------------- |
| Inbound   | `ResumeParseRequested` | `resume_id`, `tenant_id`, `parsed_text`       |
| Outbound  | `ResumeParsed`         | `resume_id`, `tenant_id`, `ai_parsed_content` |

Extracts structured data (skills, experience, education, certifications) from resume text using AI. Results are cached by content hash for 30 days.

### Screening Evaluation

| Direction | Event                          | Key Fields                                                            |
| --------- | ------------------------------ | --------------------------------------------------------------------- |
| Inbound   | `ScreeningEvaluationRequested` | `application_id`, `tenant_id`, `criteria_json`, `applicant_data_json` |
| Outbound  | `ScreeningEvaluated`           | `application_id`, `tenant_id`, `breakdown_json`, `overall_score`      |

Evaluates an applicant against job criteria and returns per-criterion scores with an overall weighted score.

### Answer Scoring

| Direction | Event                    | Key Fields                                    |
| --------- | ------------------------ | --------------------------------------------- |
| Inbound   | `AnswerScoringRequested` | `application_id`, `tenant_id`, `answers_json` |
| Outbound  | `AnswersScored`          | `application_id`, `tenant_id`, `scores_json`  |

Scores candidate free-text answers to screening questions.

### Candidate Feedback

| Direction | Event                         | Key Fields                                                                                 |
| --------- | ----------------------------- | ------------------------------------------------------------------------------------------ |
| Inbound   | `FeedbackGenerationRequested` | `application_id`, `tenant_id`, `criteria_breakdown`, `overall_score`, `transparency_level` |
| Outbound  | `FeedbackGenerated`           | `application_id`, `tenant_id`, `feedback`                                                  |

Generates candidate-facing feedback based on screening results, respecting the configured transparency level.

Transparency levels:

- **Detailed** — Detailed feedback with per-criterion analysis and improvement suggestions.
- **Summary** — High-level strengths and areas for improvement without specific scores.
- **None** — Brief, generic acknowledgment.

```
POST /api/v1/ai/criteria/suggest
```

Suggests evaluation criteria for a job posting based on title and description.

**Request Body**

| Field             | Type   | Required | Description          |
| ----------------- | ------ | -------- | -------------------- |
| `job_title`       | string | Yes      | Job title            |
| `job_description` | string | Yes      | Full job description |

**Response** `200 OK` — `list[CriteriaSuggestion]`

```json
[
  {
    "name": "Python Proficiency",
    "category": "Skill",
    "evaluation_method": "SemanticSimilarity",
    "is_required": true,
    "weight": 25.0,
    "configuration": "{}"
  }
]
```

Valid categories: `Skill`, `Experience`, `Education`, `Certification`, `Language`, `Custom`.
Valid evaluation methods: `SemanticSimilarity`, `ExactMatch`, `Custom`.

---

### Assessment Question Suggestion

```
POST /api/v1/ai/assessment/suggest
```

Suggests screening questions based on job description and evaluation criteria.

**Request Body**

| Field             | Type               | Required | Description                  |
| ----------------- | ------------------ | -------- | ---------------------------- |
| `job_description` | string             | Yes      | Full job description         |
| `criteria`        | `CriterionInput[]` | Yes      | Existing evaluation criteria |

`CriterionInput`:

| Field               | Type    | Required | Description                   |
| ------------------- | ------- | -------- | ----------------------------- |
| `id`                | UUID    | Yes      | Criterion ID                  |
| `name`              | string  | Yes      | Criterion name                |
| `category`          | string  | Yes      | Category (e.g., `Skill`)      |
| `evaluation_method` | string  | Yes      | e.g., `SemanticSimilarity`    |
| `is_required`       | boolean | Yes      | Whether criterion is required |
| `weight`            | decimal | Yes      | Weight in scoring (0–100)     |
| `configuration`     | string  | Yes      | JSON configuration string     |

**Response** `200 OK` — `list[QuestionSuggestion]`

```json
[
  {
    "question_text": "Describe your experience with async Python",
    "question_type": "FreeText",
    "timing": "AfterScreening",
    "is_required": true,
    "weight": 50.0,
    "expected_answer": "{\"key_topics\": [\"asyncio\", \"aiohttp\"]}",
    "options": null
  }
]
```

Valid question types: `FreeText`, `MultipleChoice`, `Rating`.
Valid timing: `BeforeScreening`, `AfterScreening`.

---

## Database

The AI Service uses a **shared database** with the monolith, isolated to the `ai_service` schema. Tables:

- `ai_service.ai_api_logs` — Audit log of all AI provider API calls (tokens, cost, latency, errors).
- `ai_service.parsed_resume_cache` — SHA-256 hash-based cache of parsed resume results (30-day TTL).

## JSON Conventions

- All field names use `snake_case`.
- Enum/status values use `PascalCase` (e.g., `Pass`, `Fail`, `Skill`, `FreeText`).
- Optional fields are omitted from responses when `null` (not sent as `null`).
