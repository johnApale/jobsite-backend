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

### Resume Parsing

```
POST /api/v1/ai/resumes/parse
```

Extracts structured data (skills, experience, education, certifications) from resume text using AI. Results are cached by content hash for 30 days.

**Request Body**

| Field         | Type   | Required | Description               |
| ------------- | ------ | -------- | ------------------------- |
| `parsed_text` | string | Yes      | Plain-text resume content |

**Response** `200 OK`

```json
{
  "skills": [{ "name": "Python", "level": "Advanced", "years": 5 }],
  "experience": [
    {
      "title": "Developer",
      "company": "Acme",
      "start_date": "2019-01",
      "end_date": "2024-01",
      "description": "..."
    }
  ],
  "education": [
    { "degree": "BSc Computer Science", "institution": "MIT", "field": "CS" }
  ],
  "certifications": ["AWS Solutions Architect"],
  "summary": "Experienced full-stack developer..."
}
```

All response fields are optional — absent sections are omitted from the response (not `null`).

---

### Criteria Suggestion

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

### Screening Evaluation

```
POST /api/v1/ai/screening/evaluate
```

Evaluates an applicant against job criteria and returns per-criterion scores with an overall weighted score.

**Request Body**

| Field       | Type               | Required | Description            |
| ----------- | ------------------ | -------- | ---------------------- |
| `criteria`  | `CriterionInput[]` | Yes      | Evaluation criteria    |
| `applicant` | `ApplicantInput`   | Yes      | Applicant profile data |

`ApplicantInput`:

| Field                     | Type   | Required | Description                     |
| ------------------------- | ------ | -------- | ------------------------------- |
| `profile_skills`          | string | No       | JSON string of profile skills   |
| `resume_parsed_text`      | string | No       | Plain-text resume content       |
| `resume_extracted_skills` | string | No       | JSON string of extracted skills |
| `ai_parsed_content`       | string | No       | Previously AI-parsed content    |

**Response** `200 OK`

```json
{
  "breakdown": [
    {
      "criterion_id": "3fa85f64-...",
      "criterion_name": "Python",
      "category": "Skill",
      "weight": 50.0,
      "score": 85.0,
      "result": "Pass",
      "reasoning": "Strong match based on 5 years of experience..."
    }
  ],
  "overall_score": 85.0
}
```

Result values: `Pass`, `Fail`, `Required`.

---

### Answer Scoring

```
POST /api/v1/ai/screening/score-answers
```

Scores candidate free-text answers to screening questions.

**Request Body**

| Field     | Type            | Required | Description       |
| --------- | --------------- | -------- | ----------------- |
| `answers` | `AnswerInput[]` | Yes      | Candidate answers |

`AnswerInput`:

| Field              | Type     | Required | Description                     |
| ------------------ | -------- | -------- | ------------------------------- |
| `question_id`      | UUID     | Yes      | Question ID                     |
| `question_text`    | string   | Yes      | The question asked              |
| `response_text`    | string   | Yes      | Candidate's response            |
| `scoring_guidance` | string   | No       | Additional scoring instructions |
| `key_topics`       | string[] | No       | Expected topics to cover        |

**Response** `200 OK`

```json
{
  "scores": [
    {
      "question_id": "3fa85f64-...",
      "score": 75.0,
      "result": "Pass",
      "reasoning": "Good coverage of key topics..."
    }
  ]
}
```

---

### Candidate Feedback

```
POST /api/v1/ai/screening/feedback
```

Generates candidate-facing feedback based on screening results, respecting the configured transparency level.

**Request Body**

| Field                | Type    | Required | Description                    |
| -------------------- | ------- | -------- | ------------------------------ |
| `criteria_breakdown` | string  | Yes      | JSON string of criteria scores |
| `overall_score`      | decimal | Yes      | Overall screening score        |
| `transparency_level` | string  | Yes      | `Full`, `Summary`, or `None`   |

**Response** `200 OK`

```json
{
  "feedback": "Based on your application, you demonstrated strong technical skills..."
}
```

Transparency levels:

- **Full** — Detailed feedback with per-criterion analysis and improvement suggestions.
- **Summary** — High-level strengths and areas for improvement without specific scores.
- **None** — Brief, generic acknowledgment.

---

## Database

The AI Service uses a **shared database** with the monolith, isolated to the `ai_service` schema. Tables:

- `ai_service.ai_api_logs` — Audit log of all AI provider API calls (tokens, cost, latency, errors).
- `ai_service.parsed_resume_cache` — SHA-256 hash-based cache of parsed resume results (30-day TTL).

## JSON Conventions

- All field names use `snake_case`.
- Enum/status values use `PascalCase` (e.g., `Pass`, `Fail`, `Skill`, `FreeText`).
- Optional fields are omitted from responses when `null` (not sent as `null`).
