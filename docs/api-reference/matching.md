# Matching API Reference

> Base path: `/api/v1/matching`

All endpoints require JWT authentication. Shortlist mutation endpoints require `RequireRecruiterOrAdmin` authorization policy.

---

## Candidate Matches

### GET /matches/{applicationId}

Get candidate match for an application.

**Response:** `200 OK`

```json
{
  "application_id": "uuid",
  "job_posting_id": "uuid",
  "applicant_user_id": "uuid",
  "screening_score": 82.5,
  "assessment_score": 90.0,
  "composite_score": 85.5,
  "match_strength": "Strong",
  "rank": 1,
  "screening_completed_at": "2025-01-15T10:30:00Z",
  "assessment_completed_at": "2025-01-16T14:00:00Z",
  "created_at": "2025-01-15T10:30:00Z",
  "updated_at": "2025-01-16T14:00:00Z"
}
```

**Errors:** `404` — Application has no match record.

---

### GET /matches

List candidate matches for a job posting.

**Query Parameters:**
| Parameter | Type | Required | Description |
|----------------|--------|----------|--------------------------------------|
| `job_posting_id`| uuid | Yes | Filter by job posting |
| `match_strength`| string | No | Filter: Strong, Good, Moderate, Weak |
| `cursor` | string | No | Pagination cursor |
| `page_size` | int | No | Default: 20 |

**Response:** `200 OK`

```json
{
  "items": [{ "...candidate_match..." }],
  "next_cursor": "abc123",
  "has_more": true
}
```

---

## Shortlists

### POST /shortlists

Generate a shortlist for a job posting. Selects the top-N candidates by composite score.

**Request Body:**

```json
{
  "job_posting_id": "uuid"
}
```

**Response:** `201 Created`

```json
{
  "id": "uuid",
  "job_posting_id": "uuid",
  "status": "Draft",
  "generated_by": "Algorithm",
  "total_candidates": 10,
  "candidates": [
    {
      "id": "uuid",
      "application_id": "uuid",
      "applicant_user_id": "uuid",
      "composite_score": 92.5,
      "rank": 1,
      "status": "Pending",
      "source": "Algorithm",
      "added_at": "2025-01-17T09:00:00Z"
    }
  ],
  "created_at": "2025-01-17T09:00:00Z",
  "updated_at": "2025-01-17T09:00:00Z"
}
```

**Authorization:** Recruiter or Admin only.

---

### GET /shortlists/{shortlistId}

Get a shortlist by ID, including embedded candidate details.

**Response:** `200 OK` — Same shape as POST response.

**Errors:** `404` — Shortlist not found.

---

### GET /shortlists

List shortlists for a job posting.

**Query Parameters:**
| Parameter | Type | Required | Description |
|----------------|--------|----------|--------------------------------|
| `job_posting_id`| uuid | Yes | Filter by job posting |
| `status` | string | No | Filter: Draft, Finalized |
| `cursor` | string | No | Pagination cursor |
| `page_size` | int | No | Default: 20 |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "id": "uuid",
      "job_posting_id": "uuid",
      "status": "Draft",
      "generated_by": "Algorithm",
      "total_candidates": 10,
      "created_at": "2025-01-17T09:00:00Z",
      "updated_at": "2025-01-17T09:00:00Z"
    }
  ],
  "next_cursor": null,
  "has_more": false
}
```

---

### POST /shortlists/{shortlistId}/candidates

Add a candidate manually to a draft shortlist.

**Request Body:**

```json
{
  "application_id": "uuid"
}
```

**Response:** `200 OK` — Updated shortlist with all candidates.

**Errors:**

- `404` — Shortlist or candidate match not found.
- `409` — Shortlist is finalized, or candidate already on shortlist.

**Authorization:** Recruiter or Admin only.

---

### DELETE /shortlists/{shortlistId}/candidates/{applicationId}

Soft-remove a candidate from a draft shortlist.

**Response:** `204 No Content`

**Errors:**

- `404` — Shortlist not found, or candidate not on shortlist.
- `409` — Shortlist is finalized.

**Authorization:** Recruiter or Admin only.

---

### PATCH /shortlists/{shortlistId}/candidates/{applicationId}/approve

Approve a candidate on a draft shortlist. Sets the candidate's status from `Pending` to `Approved`. Only `Approved` candidates are included when the shortlist is finalized.

**Response:** `200 OK` — Updated shortlist with all candidates.

**Errors:**

- `404` — Shortlist or candidate not found.
- `409` — Shortlist is finalized.

**Authorization:** Recruiter or Admin only.

---

### PATCH /shortlists/{shortlistId}/candidates/{applicationId}/reject

Reject a candidate on a draft shortlist. Sets the candidate's status from `Pending` to `Rejected`. Rejected candidates remain on the shortlist but are excluded from finalization.

**Response:** `200 OK` — Updated shortlist with all candidates.

**Errors:**

- `404` — Shortlist or candidate not found.
- `409` — Shortlist is finalized.

**Authorization:** Recruiter or Admin only.

---

### POST /shortlists/{shortlistId}/finalize

Lock the shortlist. Only **Approved** candidates are included in the finalized shortlist. Updates their application statuses to "Shortlisted" and publishes `CandidateShortlistedEvent` for each approved candidate. Pending and Rejected candidates are excluded.

**Response:** `200 OK` — The finalized shortlist.

**Errors:**

- `404` — Shortlist not found.
- `400` — No approved candidates on the shortlist.
- `409` — Shortlist already finalized.

**Authorization:** Recruiter or Admin only.

---

## Domain Events

### Consumed

| Event                       | Source    | Action                                                                                                              |
| --------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------- |
| `CvScreeningCompletedEvent` | Screening | Creates `CandidateMatch` with screening score. Also triggers auto-shortlist generation when configured (see below). |
| `AssessmentCompletedEvent`  | Screening | Updates existing match with assessment score                                                                        |

### Published

| Event                       | Consumer     | Trigger                                           |
| --------------------------- | ------------ | ------------------------------------------------- |
| `CandidateShortlistedEvent` | HR Workflows | Published per candidate on shortlist finalization |

---

## Configuration

Matching behavior is controlled via tenant settings (`admin.company_settings.matching_settings` JSONB):

```json
{
  "screening_weight": 60,
  "assessment_weight": 40,
  "auto_generate_shortlist": true,
  "shortlist_size": 10
}
```

### Auto-Shortlist Generation

When `auto_generate_shortlist` is `true`, the `CvScreeningCompletedMatchingHandler` automatically generates a shortlist after each screening completion. The trigger conditions are:

1. `auto_generate_shortlist` is enabled in tenant `MatchingSettings`
2. No existing **Draft** shortlist exists for the job posting
3. The number of candidate matches for the job posting meets or exceeds `shortlist_size`

All auto-generated candidates start in `Pending` status. Hiring managers must approve or reject each candidate before finalization.

### Shortlist Candidate Approval Workflow

Shortlist candidates have a `status` field with three possible values:

| Status     | Description                                          |
| ---------- | ---------------------------------------------------- |
| `Pending`  | Default. Candidate awaits review.                    |
| `Approved` | Hiring manager approved. Included in finalization.   |
| `Rejected` | Hiring manager rejected. Excluded from finalization. |

Workflow: Generate shortlist → review candidates → approve/reject → finalize (only `Approved` candidates proceed).

```

```
