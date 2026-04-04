# Recruitment Module API

## Base Path

```
/api/v1/recruitment
```

All recruitment endpoints require **tenant resolution** (subdomain in `Host` header) and **JWT authentication**.

---

## Client Company Endpoints

### Create Client Company

Create a new client company for agency-model recruiting.

```
POST /api/v1/recruitment/client-companies
```

**Authorization:** AgencyAdmin

**Request Body:**

```json
{
  "name": "Acme Technologies",
  "display_name": "Acme",
  "is_anonymous": false,
  "industry": "Technology",
  "website": "https://acme.com",
  "contact_name": "Jane Smith",
  "contact_email": "jane@acme.com",
  "contact_phone": "+1-555-0100",
  "notes": "Premium client since 2024"
}
```

**Validation Rules:**

| Field           | Rule                                                    |
| --------------- | ------------------------------------------------------- |
| `name`          | Required, max 200 characters                            |
| `industry`      | Optional; must be valid `Industry` constant if provided |
| `contact_email` | Optional; must be valid email format if provided        |

**Response:** `201 Created`

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Technologies",
  "display_name": "Acme",
  "is_anonymous": false,
  "industry": "Technology",
  "website": "https://acme.com",
  "contact_name": "Jane Smith",
  "contact_email": "jane@acme.com",
  "contact_phone": "+1-555-0100",
  "notes": "Premium client since 2024",
  "status": "Active",
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Errors:**

| Code               | Status | Condition                     |
| ------------------ | ------ | ----------------------------- |
| `VALIDATION_ERROR` | 400    | Request body fails validation |
| `UNAUTHORIZED`     | 401    | Missing or invalid JWT        |
| `FORBIDDEN`        | 403    | User is not AgencyAdmin       |

---

### List Client Companies

List client companies with cursor-based pagination.

```
GET /api/v1/recruitment/client-companies?status=Active&cursor=xxx&pageSize=20
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Query Parameters:**

| Parameter  | Type   | Description                             |
| ---------- | ------ | --------------------------------------- |
| `status`   | string | Filter by status (`Active`, `Inactive`) |
| `cursor`   | string | Opaque cursor from previous response    |
| `pageSize` | int    | Results per page (default 20, max 100)  |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Acme Technologies",
      "display_name": "Acme",
      "is_anonymous": false,
      "industry": "Technology",
      "status": "Active",
      "created_at": "2026-04-01T12:00:00Z",
      "updated_at": "2026-04-01T12:00:00Z"
    }
  ],
  "next_cursor": "eyJpZCI6IjU1MGU4NDAwLi4uIn0="
}
```

---

### Get Client Company

```
GET /api/v1/recruitment/client-companies/{id}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `200 OK` — Full client company object.

**Errors:**

| Code                       | Status | Condition         |
| -------------------------- | ------ | ----------------- |
| `CLIENT_COMPANY_NOT_FOUND` | 404    | Company not found |

---

### Update Client Company

Partially update a client company using JSON merge patch semantics.

```
PATCH /api/v1/recruitment/client-companies/{id}
```

**Authorization:** AgencyAdmin

**Request Body:** All fields optional. Only non-null values are applied.

```json
{
  "contact_email": "new-contact@acme.com",
  "status": "Inactive"
}
```

**Response:** `200 OK` — Returns the full updated client company.

**Errors:**

| Code                       | Status | Condition                     |
| -------------------------- | ------ | ----------------------------- |
| `CLIENT_COMPANY_NOT_FOUND` | 404    | Company not found             |
| `VALIDATION_ERROR`         | 400    | Request body fails validation |

---

## Job Posting Endpoints

### Create Job Posting

Create a new job posting in `Draft` status.

```
POST /api/v1/recruitment/job-postings
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:**

```json
{
  "title": "Senior .NET Developer",
  "description": "We are looking for an experienced .NET developer...",
  "location_type": "Hybrid",
  "city": "Manila",
  "country": "Philippines",
  "employment_type": "FullTime",
  "salary_min": 80000.0,
  "salary_max": 120000.0,
  "salary_currency": "USD",
  "department": "Engineering",
  "client_company_id": "550e8400-e29b-41d4-a716-446655440000",
  "closes_at": "2026-06-01T00:00:00Z"
}
```

**Validation Rules:**

| Field               | Rule                                                                               |
| ------------------- | ---------------------------------------------------------------------------------- |
| `title`             | Required, max 200 characters                                                       |
| `description`       | Required                                                                           |
| `location_type`     | Required; must be `OnSite`, `Remote`, or `Hybrid`                                  |
| `city`              | Required when `location_type` is `OnSite` or `Hybrid`                              |
| `country`           | Required when `location_type` is `OnSite` or `Hybrid`                              |
| `employment_type`   | Required; must be `FullTime`, `PartTime`, `Contract`, `Temporary`, or `Internship` |
| `salary_min`        | Must be ≥ 0 when provided                                                          |
| `salary_max`        | Must be ≥ `salary_min` when both provided                                          |
| `salary_currency`   | Required when `salary_min` or `salary_max` is provided                             |
| `client_company_id` | Must reference an existing Active client company when provided                     |

**Response:** `201 Created`

```json
{
  "id": "660e8400-e29b-41d4-a716-446655440000",
  "client_company_id": "550e8400-e29b-41d4-a716-446655440000",
  "title": "Senior .NET Developer",
  "description": "We are looking for an experienced .NET developer...",
  "location_type": "Hybrid",
  "city": "Manila",
  "country": "Philippines",
  "employment_type": "FullTime",
  "salary_min": 80000.0,
  "salary_max": 120000.0,
  "salary_currency": "USD",
  "department": "Engineering",
  "status": "Draft",
  "posted_by": "110e8400-e29b-41d4-a716-446655440000",
  "published_at": null,
  "closes_at": "2026-06-01T00:00:00Z",
  "closed_at": null,
  "criteria": [],
  "questions": [],
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Errors:**

| Code                       | Status | Condition                               |
| -------------------------- | ------ | --------------------------------------- |
| `VALIDATION_ERROR`         | 400    | Request body fails validation           |
| `CLIENT_COMPANY_NOT_FOUND` | 404    | Referenced client company doesn't exist |

---

### List Job Postings

```
GET /api/v1/recruitment/job-postings?status=Published&clientCompanyId=xxx&cursor=xxx&pageSize=20
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Query Parameters:**

| Parameter         | Type   | Description                                       |
| ----------------- | ------ | ------------------------------------------------- |
| `status`          | string | Filter by status (`Draft`, `Published`, `Closed`) |
| `clientCompanyId` | guid   | Filter by client company                          |
| `cursor`          | string | Opaque cursor from previous response              |
| `pageSize`        | int    | Results per page (default 20, max 100)            |

**Response:** `200 OK` — Paginated list of job postings with `items` and `next_cursor`.

---

### Get Job Posting

```
GET /api/v1/recruitment/job-postings/{id}
```

**Authorization:** Any authenticated user

**Response:** `200 OK` — Full job posting including `criteria` and `questions` arrays.

**Errors:**

| Code                    | Status | Condition             |
| ----------------------- | ------ | --------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found |

---

### Update Job Posting

Partially update a job posting using JSON merge patch. Only allowed while status is `Draft`.

```
PATCH /api/v1/recruitment/job-postings/{id}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:** All fields optional. Same validation rules as create.

**Response:** `200 OK` — Returns the full updated job posting.

**Errors:**

| Code                    | Status | Condition                     |
| ----------------------- | ------ | ----------------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found         |
| `VALIDATION_ERROR`      | 400    | Request body fails validation |

---

### Publish Job Posting

Transition a job posting from `Draft` to `Published`.

```
POST /api/v1/recruitment/job-postings/{id}/publish
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `200 OK` — Returns the updated job posting with `status: "Published"` and `published_at` set.

**Errors:**

| Code                    | Status | Condition                            |
| ----------------------- | ------ | ------------------------------------ |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found                |
| `INVALID_STATUS`        | 409    | Job posting is not in `Draft` status |

---

### Close Job Posting

Transition a job posting from `Published` to `Closed`.

```
POST /api/v1/recruitment/job-postings/{id}/close
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `200 OK` — Returns the updated job posting with `status: "Closed"` and `closed_at` set.

**Errors:**

| Code                    | Status | Condition                                |
| ----------------------- | ------ | ---------------------------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found                    |
| `INVALID_STATUS`        | 409    | Job posting is not in `Published` status |

---

## Evaluation Criteria Endpoints

### Add Criterion

Add an evaluation criterion to a job posting.

```
POST /api/v1/recruitment/job-postings/{jobPostingId}/criteria
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:**

```json
{
  "name": "C# Proficiency",
  "category": "Skill",
  "evaluation_method": "SemanticSimilarity",
  "is_required": true,
  "weight": 25.0,
  "configuration": "{\"keywords\": [\"C#\", \".NET\", \"ASP.NET Core\"]}",
  "display_order": 1
}
```

**Validation Rules:**

| Field               | Rule                                                                                           |
| ------------------- | ---------------------------------------------------------------------------------------------- |
| `name`              | Required, max 200 characters                                                                   |
| `category`          | Required; must be `Skill`, `Experience`, `Certification`, `Education`, `Location`, or `Custom` |
| `evaluation_method` | Required; must be `ExactMatch`, `RangeMatch`, or `SemanticSimilarity`                          |
| `weight`            | Required; must be between 0.00 and 100.00                                                      |
| `configuration`     | Required; must be valid JSON string                                                            |

**Response:** `201 Created`

```json
{
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "job_posting_id": "660e8400-e29b-41d4-a716-446655440000",
  "name": "C# Proficiency",
  "category": "Skill",
  "evaluation_method": "SemanticSimilarity",
  "is_required": true,
  "weight": 25.0,
  "configuration": "{\"keywords\": [\"C#\", \".NET\", \"ASP.NET Core\"]}",
  "display_order": 1,
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

---

### List Criteria

```
GET /api/v1/recruitment/job-postings/{jobPostingId}/criteria
```

**Authorization:** Any authenticated user

**Response:** `200 OK` — Array of criteria ordered by `display_order`.

---

### Update Criterion

```
PATCH /api/v1/recruitment/job-postings/{jobPostingId}/criteria/{criteriaId}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:** All fields optional (JSON merge patch).

**Response:** `200 OK` — Returns the full updated criterion.

**Errors:**

| Code                 | Status | Condition                                         |
| -------------------- | ------ | ------------------------------------------------- |
| `CRITERIA_NOT_FOUND` | 404    | Criterion not found or doesn't belong to this job |
| `VALIDATION_ERROR`   | 400    | Request body fails validation                     |

---

### Delete Criterion

```
DELETE /api/v1/recruitment/job-postings/{jobPostingId}/criteria/{criteriaId}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `204 No Content`

**Errors:**

| Code                 | Status | Condition           |
| -------------------- | ------ | ------------------- |
| `CRITERIA_NOT_FOUND` | 404    | Criterion not found |

---

### Suggest Criteria (AI)

Request AI-generated evaluation criteria based on the job posting's title and description.

```
POST /api/v1/recruitment/job-postings/{jobPostingId}/criteria/suggest
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `200 OK` — Array of suggested criteria (not yet persisted).

```json
[
  {
    "name": "C# Proficiency",
    "category": "Skill",
    "evaluation_method": "SemanticSimilarity",
    "is_required": true,
    "weight": 25.0,
    "configuration": "{\"keywords\": [\"C#\", \".NET\"]}"
  }
]
```

**Response:** `204 No Content` — AI service is unavailable.

**Errors:**

| Code                    | Status | Condition             |
| ----------------------- | ------ | --------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found |

---

## Screening Question Endpoints

### Add Question

Add a screening question to a job posting.

```
POST /api/v1/recruitment/job-postings/{jobPostingId}/questions
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:**

```json
{
  "question_text": "Do you have experience with microservices?",
  "question_type": "YesNo",
  "timing": "AtApplication",
  "is_required": true,
  "weight": 10.0,
  "expected_answer": "{\"expected\": \"Yes\"}",
  "options": null,
  "display_order": 1
}
```

**Validation Rules:**

| Field           | Rule                                                       |
| --------------- | ---------------------------------------------------------- |
| `question_text` | Required                                                   |
| `question_type` | Required; must be `FreeText`, `MultipleChoice`, or `YesNo` |
| `timing`        | Required; must be `AtApplication` or `AfterScreening`      |
| `weight`        | Required; must be between 0.00 and 100.00                  |

**Response:** `201 Created`

```json
{
  "id": "880e8400-e29b-41d4-a716-446655440000",
  "job_posting_id": "660e8400-e29b-41d4-a716-446655440000",
  "question_text": "Do you have experience with microservices?",
  "question_type": "YesNo",
  "timing": "AtApplication",
  "is_required": true,
  "weight": 10.0,
  "expected_answer": "{\"expected\": \"Yes\"}",
  "options": null,
  "display_order": 1,
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

---

### List Questions

```
GET /api/v1/recruitment/job-postings/{jobPostingId}/questions
```

**Authorization:** Any authenticated user

**Response:** `200 OK` — Array of questions ordered by `display_order`.

---

### Update Question

```
PATCH /api/v1/recruitment/job-postings/{jobPostingId}/questions/{questionId}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Request Body:** All fields optional (JSON merge patch).

**Response:** `200 OK` — Returns the full updated question.

**Errors:**

| Code                 | Status | Condition                                        |
| -------------------- | ------ | ------------------------------------------------ |
| `QUESTION_NOT_FOUND` | 404    | Question not found or doesn't belong to this job |
| `VALIDATION_ERROR`   | 400    | Request body fails validation                    |

---

### Delete Question

```
DELETE /api/v1/recruitment/job-postings/{jobPostingId}/questions/{questionId}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `204 No Content`

**Errors:**

| Code                 | Status | Condition          |
| -------------------- | ------ | ------------------ |
| `QUESTION_NOT_FOUND` | 404    | Question not found |

---

### Suggest Questions (AI)

Request AI-generated screening questions based on the job description and existing criteria. Feature-gated: requires both system-level gate and tenant `ai_assessment_questions_enabled` setting.

```
POST /api/v1/recruitment/job-postings/{jobPostingId}/questions/suggest
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin

**Response:** `200 OK` — Array of suggested questions (not yet persisted).

```json
[
  {
    "question_text": "Describe your experience with distributed systems.",
    "question_type": "FreeText",
    "timing": "AfterScreening",
    "is_required": true,
    "weight": 15.0,
    "expected_answer": null,
    "options": null
  }
]
```

**Response:** `204 No Content` — AI service unavailable or feature disabled.

**Errors:**

| Code                    | Status | Condition             |
| ----------------------- | ------ | --------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found |

---

## Application Endpoints

### Submit Application

Submit an application to a published job posting. One application per applicant per job posting.

```
POST /api/v1/recruitment/applications/job-postings/{jobPostingId}
```

**Authorization:** Applicant

**Request Body:**

```json
{
  "resume_id": "770e8400-e29b-41d4-a716-446655440000",
  "cover_letter_url": "/uploads/documents/cover-letter.pdf",
  "question_answers": [
    {
      "question_id": "880e8400-e29b-41d4-a716-446655440000",
      "response_text": "Yes",
      "response_data": null
    }
  ]
}
```

**Validation Rules:**

| Field                            | Rule                                                     |
| -------------------------------- | -------------------------------------------------------- |
| `resume_id`                      | Required; must reference a resume owned by the applicant |
| `question_answers[].question_id` | Required; must be non-empty GUID                         |

**Response:** `201 Created`

```json
{
  "id": "990e8400-e29b-41d4-a716-446655440000",
  "job_posting_id": "660e8400-e29b-41d4-a716-446655440000",
  "applicant_id": "110e8400-e29b-41d4-a716-446655440000",
  "status": "Submitted",
  "resume_id": "770e8400-e29b-41d4-a716-446655440000",
  "cover_letter_url": "/uploads/documents/cover-letter.pdf",
  "rejection_reason": null,
  "rejected_at_stage": null,
  "withdrawn_at": null,
  "submitted_at": "2026-04-01T12:00:00Z",
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Domain Event:** Publishes `ApplicationSubmittedEvent` via MediatR for downstream processing (Screening module).

**Errors:**

| Code                    | Status | Condition                                |
| ----------------------- | ------ | ---------------------------------------- |
| `JOB_POSTING_NOT_FOUND` | 404    | Job posting not found                    |
| `INVALID_STATUS`        | 409    | Job posting is not in `Published` status |
| `DUPLICATE_APPLICATION` | 409    | Applicant already applied to this job    |
| `RESUME_NOT_OWNED`      | 403    | Resume doesn't belong to the applicant   |
| `VALIDATION_ERROR`      | 400    | Request body fails validation            |

---

### List Applications

```
GET /api/v1/recruitment/applications?jobPostingId=xxx&status=Submitted&applicantId=xxx&cursor=xxx&pageSize=20
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin (all applications); Applicant (own applications only)

**Query Parameters:**

| Parameter      | Type   | Description                                                     |
| -------------- | ------ | --------------------------------------------------------------- |
| `jobPostingId` | guid   | Filter by job posting                                           |
| `status`       | string | Filter by status (`Submitted`, `Screening`, `Assessment`, etc.) |
| `applicantId`  | guid   | Filter by applicant                                             |
| `cursor`       | string | Opaque cursor from previous response                            |
| `pageSize`     | int    | Results per page (default 20, max 100)                          |

**Response:** `200 OK` — Paginated list with `items` and `next_cursor`.

---

### Get Application

```
GET /api/v1/recruitment/applications/{id}
```

**Authorization:** Recruiter, HiringManager, AgencyAdmin, or the Applicant who submitted

**Response:** `200 OK` — Full application object.

**Errors:**

| Code                    | Status | Condition             |
| ----------------------- | ------ | --------------------- |
| `APPLICATION_NOT_FOUND` | 404    | Application not found |

---

### Withdraw Application

Withdraw an application. Only the applicant who submitted can withdraw.

```
POST /api/v1/recruitment/applications/{id}/withdraw
```

**Authorization:** Applicant (own application only)

**Response:** `200 OK` — Returns the updated application with `status: "Withdrawn"` and `withdrawn_at` set.

**Errors:**

| Code                    | Status | Condition                        |
| ----------------------- | ------ | -------------------------------- |
| `APPLICATION_NOT_FOUND` | 404    | Application not found            |
| `FORBIDDEN`             | 403    | User is not the applicant        |
| `INVALID_STATUS`        | 409    | Application is already withdrawn |

---

## Data Models

### Job Posting Status Lifecycle

```
Draft → Published → Closed
```

### Application Status Lifecycle

```
Submitted → Screening → Assessment → Shortlisted → FinalInterview → Offered → Hired
                ↓            ↓            ↓              ↓             ↓
             Rejected     Rejected     Rejected       Rejected      Rejected

Any active status → Withdrawn (by applicant)
```

### Constants Reference

| Constant              | Valid Values                                                                                                         |
| --------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `JobPostingStatus`    | `Draft`, `Published`, `Closed`                                                                                       |
| `ApplicationStatus`   | `Submitted`, `Screening`, `Assessment`, `Shortlisted`, `FinalInterview`, `Offered`, `Hired`, `Rejected`, `Withdrawn` |
| `LocationType`        | `OnSite`, `Remote`, `Hybrid`                                                                                         |
| `EmploymentType`      | `FullTime`, `PartTime`, `Contract`, `Temporary`, `Internship`                                                        |
| `CriteriaCategory`    | `Skill`, `Experience`, `Certification`, `Education`, `Location`, `Custom`                                            |
| `EvaluationMethod`    | `ExactMatch`, `RangeMatch`, `SemanticSimilarity`                                                                     |
| `QuestionType`        | `FreeText`, `MultipleChoice`, `YesNo`                                                                                |
| `QuestionTiming`      | `AtApplication`, `AfterScreening`                                                                                    |
| `ClientCompanyStatus` | `Active`, `Inactive`                                                                                                 |
