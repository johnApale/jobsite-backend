# Screening Module API

## Base Path

```
/api/v1/screening
```

All screening endpoints require **tenant resolution** (subdomain in `Host` header) and **JWT authentication**.

---

## Screening Result Endpoints

### Get Screening Result

Retrieve the full screening result for an application, including scores, breakdowns, and routing outcome.

```
GET /api/v1/screening/results/{applicationId}
```

**Authorization:** Authenticated user

**Path Parameters:**

| Parameter       | Type   | Description          |
| --------------- | ------ | -------------------- |
| `applicationId` | `uuid` | The application's ID |

**Response:** `200 OK`

```json
{
  "application_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "overall_score": 85.5,
  "match_strength": "Strong",
  "outcome": "AutoAdvanced",
  "criteria_score_breakdown": "[{\"criterion_id\":\"...\",\"score\":90}]",
  "ai_criteria_score_breakdown": null,
  "ai_overall_score": null,
  "question_score_breakdown": "[{\"question_id\":\"...\",\"score\":100}]",
  "assessment_score": null,
  "candidate_feedback": null,
  "auto_advance_threshold": 70.0,
  "auto_reject_threshold": 30.0,
  "reviewed_by": null,
  "reviewed_at": null,
  "review_notes": null,
  "failure_reason": null,
  "started_at": "2025-01-15T10:30:00Z",
  "completed_at": "2025-01-15T10:30:02Z",
  "created_at": "2025-01-15T10:30:00Z",
  "updated_at": "2025-01-15T10:30:02Z"
}
```

**Error Responses:**

| Status | Condition                  |
| ------ | -------------------------- |
| `404`  | Screening result not found |

---

### List Screening Results

List screening results with optional filters and cursor-based pagination.

```
GET /api/v1/screening/results
```

**Authorization:** Authenticated user

**Query Parameters:**

| Parameter        | Type      | Default | Description                                                                                               |
| ---------------- | --------- | ------- | --------------------------------------------------------------------------------------------------------- |
| `status`         | `string`  | —       | Filter by status: `Pending`, `InProgress`, `Completed`, `Failed`                                          |
| `match_strength` | `string`  | —       | Filter by match strength: `Strong`, `Good`, `Moderate`, `Weak`                                            |
| `outcome`        | `string`  | —       | Filter by outcome: `AutoAdvanced`, `AutoRejected`, `ManualReview`, `ManuallyAdvanced`, `ManuallyRejected` |
| `cursor`         | `string`  | —       | Cursor for pagination (from `next_cursor` in previous response)                                           |
| `page_size`      | `integer` | `20`    | Number of results per page (max 100)                                                                      |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "application_id": "...",
      "status": "Completed",
      "overall_score": 85.5,
      "match_strength": "Strong",
      "outcome": "AutoAdvanced"
    }
  ],
  "next_cursor": "MjAyNS0wMS0xNVQxMDozMDowMFp8NTUwZTg0MDAtZTI5Yi00MWQ0LWE3MTYtNDQ2NjU1NDQwMDAw",
  "has_more": true
}
```

---

### Manual Review

Submit a manual review decision for an application that was routed to manual review.

```
POST /api/v1/screening/results/{applicationId}/review
```

**Authorization:** Recruiter or Admin (`RequireRecruiterOrAdmin` policy)

**Path Parameters:**

| Parameter       | Type   | Description          |
| --------------- | ------ | -------------------- |
| `applicationId` | `uuid` | The application's ID |

**Request Body:**

```json
{
  "outcome": "ManuallyAdvanced",
  "review_notes": "Strong candidate, advancing to next stage"
}
```

**Validation Rules:**

| Field          | Rule                                                       |
| -------------- | ---------------------------------------------------------- |
| `outcome`      | Required; must be `ManuallyAdvanced` or `ManuallyRejected` |
| `review_notes` | Optional; max 2000 characters                              |

**Response:** `200 OK` — Returns the updated `ScreeningResultResponse`

**Error Responses:**

| Status | Condition                                          |
| ------ | -------------------------------------------------- |
| `404`  | Screening result not found                         |
| `422`  | Application is not in `ManualReview` outcome state |

---

### Get Candidate Feedback

Retrieve candidate-facing transparency feedback for an application (when enabled by the tenant).

```
GET /api/v1/screening/results/{applicationId}/feedback
```

**Authorization:** Authenticated user

**Path Parameters:**

| Parameter       | Type   | Description          |
| --------------- | ------ | -------------------- |
| `applicationId` | `uuid` | The application's ID |

**Response:** `200 OK`

```json
{
  "application_id": "550e8400-e29b-41d4-a716-446655440000",
  "feedback": "Your profile shows strong alignment with the required technical skills..."
}
```

**Error Responses:**

| Status | Condition                  |
| ------ | -------------------------- |
| `404`  | Screening result not found |

> **Note:** `feedback` will be `null` if the tenant has not enabled candidate transparency or if AI feedback generation failed.

---

## Assessment Endpoints

### Submit Assessment

Submit answers to AfterScreening questions for an application. Scores are calculated and the application is routed based on the tenant's completion policy.

```
POST /api/v1/screening/assessments/{applicationId}
```

**Authorization:** Authenticated user (Applicant)

**Path Parameters:**

| Parameter       | Type   | Description          |
| --------------- | ------ | -------------------- |
| `applicationId` | `uuid` | The application's ID |

**Request Body:**

```json
{
  "job_posting_id": "660e8400-e29b-41d4-a716-446655440000",
  "answers": [
    {
      "question_id": "770e8400-e29b-41d4-a716-446655440000",
      "response_text": "My approach to system design involves...",
      "response_data": null
    },
    {
      "question_id": "880e8400-e29b-41d4-a716-446655440000",
      "response_text": null,
      "response_data": "{\"selected_options\": [0, 2]}"
    }
  ]
}
```

**Validation Rules:**

| Field            | Rule                                       |
| ---------------- | ------------------------------------------ |
| `job_posting_id` | Required; must be a valid UUID             |
| `answers`        | Required; must contain at least one answer |
| `question_id`    | Required per answer; must be a valid UUID  |

**Response:** `204 No Content`

**Error Responses:**

| Status | Condition                          |
| ------ | ---------------------------------- |
| `400`  | Invalid request body or validation |
| `404`  | Screening result not found         |
| `409`  | Assessment already submitted       |

---

### Get Assessment Status

Check the assessment status for an application and retrieve the AfterScreening questions to answer.

```
GET /api/v1/screening/assessments/{applicationId}?jobPostingId={jobPostingId}
```

**Authorization:** Authenticated user

**Query Parameters:**

| Parameter      | Type   | Description          |
| -------------- | ------ | -------------------- |
| `jobPostingId` | `uuid` | The job posting's ID |

**Response:** `200 OK`

```json
{
  "is_submitted": false,
  "questions": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440000",
      "question_text": "Describe your approach to system design.",
      "question_type": "FreeText",
      "is_required": true,
      "options": null
    }
  ]
}
```

**Error Responses:**

| Status | Condition                  |
| ------ | -------------------------- |
| `404`  | Screening result not found |
| `422`  | Assessment not available   |

---

## Domain Events

The Screening module publishes and consumes the following domain events via the in-process event bus:

### Consumed Events

| Event                       | Source      | Action                                                                        |
| --------------------------- | ----------- | ----------------------------------------------------------------------------- |
| `ApplicationSubmittedEvent` | Recruitment | Creates screening result, stores AtApplication answers, runs scoring pipeline |

### Published Events

| Event                       | Consumer(s) | Payload                                                                              |
| --------------------------- | ----------- | ------------------------------------------------------------------------------------ |
| `CvScreeningCompletedEvent` | —           | `ApplicationId`, `JobPostingId`, `OverallScore`, `Outcome`                           |
| `AssessmentCompletedEvent`  | Admin       | `ApplicationId`, `JobPostingId`, `ApplicantUserId`, `AssessmentScore`, `CompletedAt` |

---

## Scoring Pipeline

The screening scoring pipeline runs in the following order:

1. **Deterministic Scoring** — Always runs. Evaluates criteria using ExactMatch, RangeMatch, and SemanticSimilarity rules. Drives all routing decisions.
2. **AI Scoring** — Runs only when enabled (tenant `ai_scoring_enabled` flag). Calls AI Service for per-criterion analysis. Results stored alongside deterministic scores for side-by-side comparison.
3. **Question Scoring** — Scores AtApplication question answers. MultipleChoice/YesNo scored deterministically. FreeText always scored via AI (independent of AI scoring flag).
4. **Three-tier Routing** — Based on deterministic `overall_score`:
   - Above auto-advance threshold → `AutoAdvanced` (route to Assessment if AfterScreening questions exist, else Shortlisted)
   - Below auto-reject threshold → `AutoRejected`
   - Between thresholds → Apply `manual_review_policy` (QueueForReview, AutoAdvanceAll, AutoRejectAll, NotifyAndHold)
5. **Candidate Transparency** — When enabled, generates candidate-facing feedback via AI Service.

---

## Constants

### Screening Status

`Pending` | `InProgress` | `Completed` | `Failed`

### Match Strength

`Strong` (≥80) | `Good` (≥60) | `Moderate` (≥40) | `Weak` (<40)

### Screening Outcome

`AutoAdvanced` | `AutoRejected` | `ManualReview` | `ManuallyAdvanced` | `ManuallyRejected`

### Score Result

`MeetsRequirement` (≥80) | `PartialMatch` (≥40) | `Missing` (<40)

### Manual Review Policy

`QueueForReview` | `AutoAdvanceAll` | `AutoRejectAll` | `NotifyAndHold`
