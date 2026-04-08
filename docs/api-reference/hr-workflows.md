# HR Workflows API Reference

> Base path: `/api/v1/hr-workflows`

All endpoints require JWT authentication. Mutation endpoints require `RequireRecruiterOrAdmin` authorization policy.

---

## Final Interviews

### POST /interviews

Schedule a final interview for a shortlisted candidate with assigned panelists.

**Request Body:**

```json
{
  "application_id": "uuid",
  "interview_type": "Video",
  "scheduled_at": "2025-02-01T10:00:00Z",
  "duration_minutes": 60,
  "location": "Zoom meeting link",
  "panelist_user_ids": ["uuid", "uuid"]
}
```

**Response:** `201 Created`

```json
{
  "application_id": "uuid",
  "status": "Scheduled",
  "interview_type": "Video",
  "scheduled_at": "2025-02-01T10:00:00Z",
  "duration_minutes": 60,
  "location": "Zoom meeting link",
  "scheduled_by": "uuid",
  "overall_recommendation": null,
  "decision_notes": null,
  "decided_by": null,
  "decided_at": null,
  "completed_at": null,
  "cancelled_at": null,
  "cancellation_reason": null,
  "aggregated_recommendation": null,
  "panelists": [
    {
      "id": "uuid",
      "interviewer_id": "uuid",
      "rating": null,
      "recommendation": null,
      "strengths": null,
      "concerns": null,
      "notes": null,
      "feedback_submitted_at": null,
      "created_at": "2025-01-30T09:00:00Z"
    }
  ],
  "created_at": "2025-01-30T09:00:00Z",
  "updated_at": "2025-01-30T09:00:00Z"
}
```

**Authorization:** Recruiter or Admin only.

**Side effects:** Publishes `FinalInterviewScheduledEvent`. Updates application status to `FinalInterview`.

**Errors:** `409` — Interview already scheduled for this application.

---

### GET /interviews/{applicationId}

Get a final interview by application ID, including all panelist details.

**Response:** `200 OK` — Same shape as POST response.

**Errors:** `404` — Interview not found.

---

### GET /interviews

List final interviews with optional filtering.

**Query Parameters:**

| Parameter   | Type   | Required | Description                                                 |
| ----------- | ------ | -------- | ----------------------------------------------------------- |
| `status`    | string | No       | Filter: Scheduled, InProgress, Completed, Cancelled, NoShow |
| `cursor`    | string | No       | Pagination cursor                                           |
| `page_size` | int    | No       | Default: 20                                                 |

**Response:** `200 OK`

```json
{
  "items": [{ "...final_interview..." }],
  "next_cursor": "abc123",
  "has_more": true
}
```

---

### PATCH /interviews/{applicationId}

Update interview details (type, scheduled time, duration, location). Only allowed when status is `Scheduled`.

**Request Body:**

```json
{
  "interview_type": "InPerson",
  "scheduled_at": "2025-02-05T14:00:00Z",
  "duration_minutes": 90,
  "location": "Conference Room A"
}
```

**Response:** `200 OK` — Updated interview response.

**Authorization:** Recruiter or Admin only.

**Errors:** `404` — Not found. `409` — Interview already completed or cancelled.

---

### POST /interviews/{applicationId}/feedback

Submit panelist feedback for an interview. The authenticated user must be an assigned panelist.

**Request Body:**

```json
{
  "rating": 4.5,
  "recommendation": "StrongHire",
  "strengths": "Excellent communication and problem-solving skills",
  "concerns": "Limited experience with distributed systems",
  "notes": "Would be a great addition to the team"
}
```

**Response:** `200 OK` — Updated interview response (includes aggregated recommendation if all panelists have submitted).

**Side effects:** When all panelists have submitted feedback, the interview auto-completes and computes the aggregated recommendation via majority vote.

**Errors:** `404` — Interview not found or user is not a panelist. `409` — Feedback already submitted.

**Validation:**

- `rating`: Required, 1.0–5.0
- `recommendation`: Required, one of `StrongHire`, `Hire`, `NoHire`, `StrongNoHire`

---

### POST /interviews/{applicationId}/decision

Record a hiring decision for a completed interview.

**Request Body:**

```json
{
  "overall_recommendation": "Hire",
  "decision_notes": "Strong candidate, proceeding to offer stage"
}
```

**Response:** `200 OK` — Updated interview response.

**Authorization:** Recruiter or Admin only.

**Side effects:** If recommendation is negative (`NoHire`/`StrongNoHire`), the application status is updated to `Rejected`.

**Errors:** `404` — Not found. `409` — Interview not in a decidable state.

---

### POST /interviews/{applicationId}/cancel

Cancel a scheduled interview.

**Request Body:**

```json
{
  "reason": "Candidate withdrew application"
}
```

**Response:** `200 OK` — Updated interview response.

**Authorization:** Recruiter or Admin only.

**Errors:** `404` — Not found. `409` — Interview already completed or already cancelled.

---

## Job Offers

### POST /offers

Create a draft job offer for an application.

**Request Body:**

```json
{
  "application_id": "uuid",
  "salary": 95000.0,
  "salary_currency": "USD",
  "salary_period": "Annual",
  "employment_type": "FullTime",
  "start_date": "2025-03-01T00:00:00Z",
  "benefits": "Health insurance, 401k match, PTO",
  "additional_terms": "Remote work allowed 3 days/week",
  "offer_letter_url": "https://storage.example.com/offers/letter.pdf",
  "expires_at": "2025-02-15T23:59:59Z",
  "client_company_id": "uuid"
}
```

**Response:** `201 Created`

```json
{
  "application_id": "uuid",
  "client_company_id": "uuid",
  "status": "Draft",
  "salary": 95000.0,
  "salary_currency": "USD",
  "salary_period": "Annual",
  "employment_type": "FullTime",
  "start_date": "2025-03-01T00:00:00Z",
  "benefits": "Health insurance, 401k match, PTO",
  "additional_terms": "Remote work allowed 3 days/week",
  "offer_letter_url": "https://storage.example.com/offers/letter.pdf",
  "expires_at": "2025-02-15T23:59:59Z",
  "extended_by": "uuid",
  "extended_at": null,
  "responded_at": null,
  "decline_reason": null,
  "withdrawn_at": null,
  "withdrawal_reason": null,
  "created_at": "2025-01-30T09:00:00Z",
  "updated_at": "2025-01-30T09:00:00Z"
}
```

**Authorization:** Recruiter or Admin only.

**Errors:** `409` — Offer already exists for this application.

---

### GET /offers/{applicationId}

Get a job offer by application ID.

**Response:** `200 OK` — Same shape as POST response.

**Errors:** `404` — Offer not found.

---

### GET /offers

List job offers with optional filtering.

**Query Parameters:**

| Parameter   | Type   | Required | Description                                                    |
| ----------- | ------ | -------- | -------------------------------------------------------------- |
| `status`    | string | No       | Filter: Draft, Pending, Accepted, Declined, Withdrawn, Expired |
| `cursor`    | string | No       | Pagination cursor                                              |
| `page_size` | int    | No       | Default: 20                                                    |

**Response:** `200 OK`

```json
{
  "items": [{ "...job_offer..." }],
  "next_cursor": "abc123",
  "has_more": true
}
```

---

### PATCH /offers/{applicationId}

Update a draft offer. Only allowed when status is `Draft`.

**Request Body:**

```json
{
  "salary": 100000.0,
  "salary_currency": "USD",
  "salary_period": "Annual",
  "employment_type": "FullTime",
  "start_date": "2025-03-15T00:00:00Z",
  "benefits": "Updated benefits package",
  "additional_terms": "Updated terms",
  "offer_letter_url": "https://storage.example.com/offers/letter-v2.pdf",
  "expires_at": "2025-02-28T23:59:59Z"
}
```

**Response:** `200 OK` — Updated offer response.

**Authorization:** Recruiter or Admin only.

**Errors:** `404` — Not found. `409` — Offer is not in Draft status.

---

### POST /offers/{applicationId}/extend

Extend a draft offer to the candidate. Transitions status from `Draft` to `Pending`.

**Response:** `200 OK` — Updated offer response with `status: "Pending"` and `extended_at` timestamp.

**Authorization:** Recruiter or Admin only.

**Side effects:** Publishes `OfferExtendedEvent`. Updates application status to `OfferExtended`.

**Errors:** `404` — Not found. `409` — Offer is not in Draft status.

---

### POST /offers/{applicationId}/respond

Candidate responds to a pending offer (accept or decline).

**Request Body:**

```json
{
  "accepted": true,
  "decline_reason": null
}
```

**Response:** `200 OK` — Updated offer response.

**Side effects:**

- Accept: Status → `Accepted`, application status → `Hired`
- Decline: Status → `Declined`, application status → `Rejected`

**Errors:** `404` — Not found. `409` — Offer is not in Pending status.

---

### POST /offers/{applicationId}/withdraw

Withdraw a pending or draft offer.

**Request Body:**

```json
{
  "withdrawal_reason": "Position filled internally"
}
```

**Response:** `200 OK` — Updated offer response.

**Authorization:** Recruiter or Admin only.

**Errors:** `404` — Not found. `409` — Offer already responded to (accepted/declined).

---

## Status Enums

### Interview Status

| Value        | Description                       |
| ------------ | --------------------------------- |
| `Scheduled`  | Interview is scheduled            |
| `InProgress` | Interview is currently happening  |
| `Completed`  | Interview completed with feedback |
| `Cancelled`  | Interview was cancelled           |
| `NoShow`     | Candidate did not attend          |

### Interview Type

| Value      | Description       |
| ---------- | ----------------- |
| `InPerson` | In-person meeting |
| `Video`    | Video call        |
| `Phone`    | Phone call        |

### Interview Recommendation

| Value          | Description              |
| -------------- | ------------------------ |
| `StrongHire`   | Strongly recommended     |
| `Hire`         | Recommended              |
| `NoHire`       | Not recommended          |
| `StrongNoHire` | Strongly not recommended |

### Offer Status

| Value       | Description                       |
| ----------- | --------------------------------- |
| `Draft`     | Offer being prepared              |
| `Pending`   | Offer extended, awaiting response |
| `Accepted`  | Candidate accepted the offer      |
| `Declined`  | Candidate declined the offer      |
| `Withdrawn` | Offer withdrawn by employer       |
| `Expired`   | Offer expired without response    |

### Salary Period

| Value     | Description    |
| --------- | -------------- |
| `Annual`  | Yearly salary  |
| `Monthly` | Monthly salary |
| `Hourly`  | Hourly rate    |

### Employment Type

| Value       | Description        |
| ----------- | ------------------ |
| `FullTime`  | Full-time position |
| `PartTime`  | Part-time position |
| `Contract`  | Contract position  |
| `Temporary` | Temporary position |
