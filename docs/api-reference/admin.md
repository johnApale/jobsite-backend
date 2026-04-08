# Admin Module API

## Base Path

```
/api/v1/admin
```

All admin endpoints require **tenant resolution** (subdomain in `Host` header) and **JWT authentication** with the `RequireAgencyAdmin` policy.

## Endpoints

### Get Dashboard Statistics

Retrieve aggregate pipeline statistics across recruitment, screening, and matching modules.

```
GET /api/v1/admin/dashboard
```

**Authorization:** `RequireAgencyAdmin`

**Response:** `200 OK`

```json
{
  "recruitment": {
    "total_job_postings": 25,
    "active_job_postings": 12,
    "closed_job_postings": 8,
    "total_applications": 150,
    "submitted_applications": 30,
    "screening_applications": 20,
    "shortlisted_applications": 15,
    "rejected_applications": 40,
    "hired_applications": 10,
    "withdrawn_applications": 5
  },
  "screening": {
    "total_screenings": 120,
    "completed_screenings": 100,
    "pending_screenings": 15,
    "failed_screenings": 5,
    "average_score": 72.50,
    "auto_advanced_count": 60,
    "auto_rejected_count": 20,
    "manual_review_count": 20
  },
  "matching": {
    "total_shortlists": 10,
    "draft_shortlists": 3,
    "finalized_shortlists": 7,
    "total_candidate_matches": 85
  }
}
```

**Errors:**

| Code           | Status | Condition                             |
| -------------- | ------ | ------------------------------------- |
| `UNAUTHORIZED` | 401    | Missing or invalid JWT                |
| `FORBIDDEN`    | 403    | User does not have `AgencyAdmin` role |

---

### Get Settings

Retrieve the current tenant company settings.

```
GET /api/v1/admin/settings
```

**Authorization:** `RequireAgencyAdmin`

**Response:** `200 OK`

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "default_timezone": "UTC",
  "default_currency": "USD",
  "auth_settings": {
    "enabled_oauth_providers": ["Google", "Apple", "Facebook"],
    "allow_self_registration": true,
    "require_email_verification": true,
    "password_min_length": 8
  },
  "profile_settings": {
    "required_profile_fields": ["phone", "skills"],
    "required_social_links": ["linkedin"],
    "required_documents": ["CoverLetter"],
    "minimum_skills_count": 3,
    "resume_required": true,
    "ai_parsing_enabled": true,
    "ai_parsing_provider": "OpenAI"
  },
  "screening_settings": {
    "auto_advance_threshold": 70.0,
    "auto_reject_threshold": 30.0,
    "manual_review_policy": "QueueForReview",
    "ai_scoring_enabled": false,
    "candidate_transparency_enabled": false,
    "candidate_transparency_level": "Summary",
    "default_evaluation_criteria": [
      {
        "name": "Skills Match",
        "category": "Skill",
        "evaluation_method": "SemanticSimilarity",
        "is_required": true,
        "weight": 40
      }
    ]
  },
  "matching_settings": {
    "screening_weight": 100,
    "assessment_weight": 0,
    "auto_generate_shortlist": true,
    "shortlist_size": 10
  },
  "assessment_settings": {
    "enabled": true,
    "time_limit_minutes": 60,
    "allow_skip": true,
    "partial_completion_policy": "ScorePartial",
    "completion_policy": "AutoAdvance",
    "ai_assessment_questions_enabled": false
  },
  "notification_settings": {
    "notify_on_new_application": true,
    "notify_on_screening_complete": true,
    "notify_on_assessment_complete": true,
    "notify_on_manual_review_needed": true,
    "notify_on_offer_response": true,
    "notification_email": null
  },
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Errors:**

| Code                 | Status | Condition                              |
| -------------------- | ------ | -------------------------------------- |
| `SETTINGS_NOT_FOUND` | 404    | No settings row exists for this tenant |
| `UNAUTHORIZED`       | 401    | Missing or invalid JWT                 |
| `FORBIDDEN`          | 403    | User does not have `AgencyAdmin` role  |

---

### Update Settings

Partially update tenant company settings using JSON merge patch semantics. Only non-null fields in the request body are applied.

```
PATCH /api/v1/admin/settings
```

**Authorization:** `RequireAgencyAdmin`

**Request Body:**

All fields are optional. Only provided (non-null) fields are updated.

```json
{
  "default_timezone": "America/New_York",
  "screening_settings": {
    "auto_advance_threshold": 75.0,
    "auto_reject_threshold": 25.0,
    "manual_review_policy": "AutoAdvanceAll"
  },
  "matching_settings": {
    "screening_weight": 60,
    "assessment_weight": 40
  }
}
```

**Validation Rules:**

| Field                                             | Rule                                                                 |
| ------------------------------------------------- | -------------------------------------------------------------------- |
| `default_timezone`                                | Max 50 characters                                                    |
| `default_currency`                                | Exactly 3 characters (ISO 4217)                                      |
| `auth_settings.password_min_length`               | Between 6 and 128                                                    |
| `profile_settings.minimum_skills_count`           | 0 or greater                                                         |
| `profile_settings.ai_parsing_provider`            | `OpenAI`, `Anthropic`, or `AzureOpenAI`                              |
| `screening_settings.auto_advance_threshold`       | Between 0 and 100                                                    |
| `screening_settings.auto_reject_threshold`        | Between 0 and 100                                                    |
| `screening_settings.manual_review_policy`         | `QueueForReview`, `AutoAdvanceAll`, `AutoRejectAll`, `NotifyAndHold` |
| `screening_settings.candidate_transparency_level` | `None`, `Summary`, or `Detailed`                                     |
| `matching_settings.screening_weight`              | Between 0 and 100                                                    |
| `matching_settings.assessment_weight`             | Between 0 and 100                                                    |
| `matching_settings.shortlist_size`                | Greater than 0                                                       |
| `assessment_settings.time_limit_minutes`          | Greater than 0                                                       |
| `assessment_settings.partial_completion_policy`   | `ScorePartial` or `MarkIncomplete`                                   |
| `assessment_settings.completion_policy`           | `AutoAdvance` or `QueueForReview`                                    |

**Response:** `200 OK` — Returns the full updated settings (same shape as GET).

**Errors:**

| Code                 | Status | Condition                              |
| -------------------- | ------ | -------------------------------------- |
| `SETTINGS_NOT_FOUND` | 404    | No settings row exists for this tenant |
| `VALIDATION_ERROR`   | 400    | Request body fails validation          |
| `UNAUTHORIZED`       | 401    | Missing or invalid JWT                 |
| `FORBIDDEN`          | 403    | User does not have `AgencyAdmin` role  |

---

### Query Audit Logs

Retrieve paginated audit log entries with optional filters.

```
GET /api/v1/admin/audit-logs
```

**Authorization:** `RequireAgencyAdmin`

**Query Parameters:**

| Parameter     | Type       | Required | Description                                          |
| ------------- | ---------- | -------- | ---------------------------------------------------- |
| `action`      | `string`   | No       | Filter by action type (e.g., `UserRegistered`)       |
| `actor_id`    | `uuid`     | No       | Filter by actor user ID                              |
| `entity_type` | `string`   | No       | Filter by entity type (e.g., `User`)                 |
| `date_from`   | `datetime` | No       | Filter by start date (inclusive, ISO 8601)           |
| `date_to`     | `datetime` | No       | Filter by end date (inclusive, ISO 8601)             |
| `cursor`      | `string`   | No       | Opaque cursor from previous response's `next_cursor` |
| `page_size`   | `int`      | No       | Results per page (default: 20, max: 100)             |

**Response:** `200 OK`

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "actor_id": "660e8400-e29b-41d4-a716-446655440000",
      "actor_email": "admin@acme.com",
      "actor_role": "AgencyAdmin",
      "action": "SettingsUpdated",
      "entity_type": "CompanySettings",
      "entity_id": "770e8400-e29b-41d4-a716-446655440000",
      "details": {
        "changed_fields": ["default_timezone", "screening_settings"]
      },
      "ip_address": "192.168.1.1",
      "user_agent": "Mozilla/5.0...",
      "performed_at": "2026-04-01T14:30:00Z",
      "created_at": "2026-04-01T14:30:00Z"
    }
  ],
  "next_cursor": "eyJwZXJmb3JtZWRfYXQiOi..."
}
```

**Known Audit Actions:**

| Action                    | Entity Type       | Triggered By               |
| ------------------------- | ----------------- | -------------------------- |
| `UserRegistered`          | `User`            | User registration          |
| `SettingsUpdated`         | `CompanySettings` | Admin settings update      |
| `ApplicationSubmitted`    | `Application`     | Application submission     |
| `CvScreeningCompleted`    | `ScreeningResult` | Screening pipeline         |
| `CandidateShortlisted`    | `Application`     | Matching/shortlisting      |
| `FinalInterviewScheduled` | `FinalInterview`  | HR Workflow                |
| `OfferExtended`           | `JobOffer`        | HR Workflow                |
| `TenantProvisioned`       | `Tenant`          | Tenant provisioning system |

**Errors:**

| Code           | Status | Condition                             |
| -------------- | ------ | ------------------------------------- |
| `UNAUTHORIZED` | 401    | Missing or invalid JWT                |
| `FORBIDDEN`    | 403    | User does not have `AgencyAdmin` role |

---

## Settings Blocks Reference

### Auth Settings

Controls authentication behavior for the tenant.

| Field                        | Type       | Default                           | Description                          |
| ---------------------------- | ---------- | --------------------------------- | ------------------------------------ |
| `enabled_oauth_providers`    | `string[]` | `["Google", "Apple", "Facebook"]` | Available OAuth login providers      |
| `allow_self_registration`    | `bool`     | `true`                            | Allow public user registration       |
| `require_email_verification` | `bool`     | `true`                            | Require email verification on signup |
| `password_min_length`        | `int`      | `8`                               | Minimum password length (6–128)      |

### Profile Settings

Controls applicant profile requirements.

| Field                     | Type       | Default               | Description                                 |
| ------------------------- | ---------- | --------------------- | ------------------------------------------- |
| `required_profile_fields` | `string[]` | `["phone", "skills"]` | Profile fields that must be filled          |
| `required_social_links`   | `string[]` | `["linkedin"]`        | Required social media links                 |
| `required_documents`      | `string[]` | `["CoverLetter"]`     | Required document types                     |
| `minimum_skills_count`    | `int`      | `3`                   | Min number of skills on profile             |
| `resume_required`         | `bool`     | `true`                | Whether resume upload is mandatory          |
| `ai_parsing_enabled`      | `bool`     | `true`                | Enable AI-powered resume parsing            |
| `ai_parsing_provider`     | `string`   | `"OpenAI"`            | AI provider: OpenAI, Anthropic, AzureOpenAI |

### Screening Settings

Controls the automated screening pipeline behavior.

| Field                            | Type                    | Default            | Description                               |
| -------------------------------- | ----------------------- | ------------------ | ----------------------------------------- |
| `auto_advance_threshold`         | `double`                | `70.0`             | Min score to auto-advance (0–100)         |
| `auto_reject_threshold`          | `double`                | `30.0`             | Max score to auto-reject (0–100)          |
| `manual_review_policy`           | `string`                | `"QueueForReview"` | Policy for gray-zone scores               |
| `ai_scoring_enabled`             | `bool`                  | `false`            | Enable AI-powered scoring alongside rules |
| `candidate_transparency_enabled` | `bool`                  | `false`            | Show evaluation feedback to candidates    |
| `candidate_transparency_level`   | `string`                | `"Summary"`        | Feedback detail: None, Summary, Detailed  |
| `default_evaluation_criteria`    | `EvaluationCriterion[]` | `[]`               | Default criteria for new job postings     |

### Matching Settings

Controls candidate ranking and shortlisting.

| Field                     | Type   | Default | Description                              |
| ------------------------- | ------ | ------- | ---------------------------------------- |
| `screening_weight`        | `int`  | `100`   | Weight for screening score (0–100)       |
| `assessment_weight`       | `int`  | `0`     | Weight for assessment score (0–100)      |
| `auto_generate_shortlist` | `bool` | `true`  | Auto-generate shortlists                 |
| `shortlist_size`          | `int`  | `10`    | Default number of shortlisted candidates |

### Assessment Settings

Controls the optional assessment phase.

| Field                             | Type     | Default          | Description                         |
| --------------------------------- | -------- | ---------------- | ----------------------------------- |
| `enabled`                         | `bool`   | `true`           | Enable assessment phase             |
| `time_limit_minutes`              | `int`    | `60`             | Time limit for assessments          |
| `allow_skip`                      | `bool`   | `true`           | Allow candidates to skip assessment |
| `partial_completion_policy`       | `string` | `"ScorePartial"` | ScorePartial or MarkIncomplete      |
| `completion_policy`               | `string` | `"AutoAdvance"`  | AutoAdvance or QueueForReview       |
| `ai_assessment_questions_enabled` | `bool`   | `false`          | Enable AI-generated questions       |

### Notification Settings

Controls email notification preferences.

| Field                            | Type     | Default | Description                             |
| -------------------------------- | -------- | ------- | --------------------------------------- |
| `notify_on_new_application`      | `bool`   | `true`  | Notify when application is received     |
| `notify_on_screening_complete`   | `bool`   | `true`  | Notify when screening finishes          |
| `notify_on_assessment_complete`  | `bool`   | `true`  | Notify when assessment finishes         |
| `notify_on_manual_review_needed` | `bool`   | `true`  | Notify when manual review is required   |
| `notify_on_offer_response`       | `bool`   | `true`  | Notify when candidate responds to offer |
| `notification_email`             | `string` | `null`  | Override email for notifications        |
