# Admin Module — Database Design

Tenant-scoped administration. Manages company settings, tenant configuration, and audit logging. This is the control plane for each tenant — everything from branding and OAuth provider toggles to screening thresholds and assessment settings lives here.

The Admin module also provides dashboard aggregation by reading from other modules' tables, but it doesn't own those tables — it performs read-only queries across schemas. The two tables it owns are `company_settings` and `audit_logs`.

A separate `PlatformAdminController` exists for system-wide operations (managing tenants, subscriptions). Those operations touch the Catalog DB, not the tenant DB — they're not covered in this design.

---

## Tables

### company_settings

Tenant-scoped configuration. One row per tenant (per database). This is the single source of truth for all tenant-configurable behavior across the platform — profile requirements, screening thresholds, matching weights, assessment settings, OAuth toggles, and branding overrides.

Uses a singleton pattern — there's exactly one row in this table per tenant database, created during tenant provisioning with default values. No PK/FK to another table — the tenant boundary is the database itself.

| Column                | Type        | Constraints             | Description                                                                                               |
| --------------------- | ----------- | ----------------------- | --------------------------------------------------------------------------------------------------------- |
| id                    | uuid        | PK                      | Singleton — one row per tenant database                                                                   |
| default_timezone      | varchar(50) | NOT NULL, DEFAULT 'UTC' | IANA timezone (e.g., `America/New_York`). Used for scheduling, deadlines, and display                     |
| default_currency      | varchar(3)  | NOT NULL, DEFAULT 'USD' | ISO 4217 currency code. Default for salary fields on job postings                                         |
| auth_settings         | jsonb       | NOT NULL                | OAuth provider toggles and auth configuration. See Auth Settings below                                    |
| profile_settings      | jsonb       | NOT NULL                | Required profile fields, social links, documents. See Profile Settings below                              |
| screening_settings    | jsonb       | NOT NULL                | Thresholds, review policy, AI scoring, transparency, default criteria. See Screening Settings below       |
| matching_settings     | jsonb       | NOT NULL                | Screening/assessment weight split. See Matching Settings below                                            |
| assessment_settings   | jsonb       | NOT NULL                | Assessment phase configuration, completion policy, AI question suggestions. See Assessment Settings below |
| notification_settings | jsonb       | NOT NULL                | Email notification preferences. See Notification Settings below                                           |
| created_at            | timestamp   | NOT NULL                |                                                                                                           |
| updated_at            | timestamp   | NOT NULL                | Auto-set on modification                                                                                  |

No indexes needed — this is a single-row table accessed by PK.

---

### audit_logs

Tracks who did what across the tenant. Every significant action — user management, settings changes, pipeline decisions, data access — gets an audit entry. This is append-only: rows are never updated or deleted.

The audit log is written by listening to domain events from other modules and by direct instrumentation in the Admin module's own operations.

| Column       | Type         | Constraints | Description                                                                                                                                              |
| ------------ | ------------ | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| id           | uuid         | PK          |                                                                                                                                                          |
| actor_id     | uuid         | NOT NULL    | The user who performed the action. References `auth.users.id` but stored as a plain UUID (not a FK) to ensure audit entries survive user deletion        |
| actor_email  | varchar(254) | NOT NULL    | Denormalized email at time of action. If the user's email changes later, the audit record still shows who acted                                          |
| actor_role   | varchar(20)  | NOT NULL    | Denormalized role at time of action. Same reason — captures the role they had when they did it                                                           |
| action       | varchar(100) | NOT NULL    | What was done (e.g., `UserInvited`, `UserDeactivated`, `JobPostingPublished`, `SettingsUpdated`, `ScreeningReviewed`, `OfferExtended`, `OfferWithdrawn`) |
| entity_type  | varchar(50)  | NOT NULL    | What type of entity was affected (e.g., `User`, `JobPosting`, `Application`, `ScreeningResult`, `JobOffer`, `CompanySettings`)                           |
| entity_id    | uuid         | nullable    | The ID of the affected entity. NULL for actions that don't target a specific record (e.g., bulk operations)                                              |
| details      | jsonb        | nullable    | Structured context about the action. Contents vary by action type. See Audit Details below                                                               |
| ip_address   | varchar(45)  | nullable    | Client IP address at time of action. Supports IPv4 and IPv6                                                                                              |
| user_agent   | varchar(500) | nullable    | Client user agent string. Useful for identifying access patterns                                                                                         |
| performed_at | timestamp    | NOT NULL    | When the action occurred. Distinct from `created_at` for clarity (though they'll usually be identical)                                                   |
| created_at   | timestamp    | NOT NULL    |                                                                                                                                                          |

**Indexes:**

| Name                       | Columns                | Type       | Purpose                                                |
| -------------------------- | ---------------------- | ---------- | ------------------------------------------------------ |
| ix_audit_logs_actor_id     | actor_id               | Non-unique | "What did this user do?" — user activity history       |
| ix_audit_logs_action       | action                 | Non-unique | Filter by action type (e.g., "all user deactivations") |
| ix_audit_logs_entity       | entity_type, entity_id | Non-unique | "What happened to this entity?" — entity history       |
| ix_audit_logs_performed_at | performed_at           | Non-unique | Time-range queries, recent activity, cleanup           |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS admin;
```

All Admin module tables live under the `admin` schema.

---

## Relationships

```
company_settings : singleton (one row per tenant database)
audit_logs : append-only, no FKs (actor_id is a plain UUID, not a FK)
```

The audit log deliberately has no foreign keys. `actor_id` is stored as a plain UUID so that audit entries survive user deletion — if an admin deactivates and eventually deletes a user, the record of what that user did must persist. `actor_email` and `actor_role` are denormalized for the same reason.

---

## Settings Formats

Each JSONB settings column on `company_settings` has a defined structure. Default values are seeded during tenant provisioning. Tenants can override any setting through the admin portal.

### Auth Settings

```json
{
  "enabled_oauth_providers": ["Google", "Apple", "Facebook"],
  "allow_self_registration": true,
  "require_email_verification": true,
  "password_min_length": 8
}
```

**`enabled_oauth_providers`**: Which OAuth providers are shown on the login page. Platform-level OAuth app credentials are in `appsettings.json` — this just controls which providers this tenant enables. Empty array = email/password only.

**`allow_self_registration`**: Whether applicants can self-register or must be invited. Default true.

**`require_email_verification`**: Whether email verification is required before the applicant can apply. OAuth sign-ups with verified emails bypass this.

**`password_min_length`**: Minimum password length for email/password accounts. Default 8.

---

### Profile Settings

```json
{
  "required_profile_fields": ["phone", "skills"],
  "required_social_links": ["linkedin"],
  "required_documents": ["CoverLetter"],
  "minimum_skills_count": 3,
  "resume_required": true,
  "ai_parsing_enabled": true,
  "ai_parsing_provider": "OpenAI"
}
```

**`required_profile_fields`**: Which profile fields the applicant must fill in. Valid values: `phone`, `city`, `country`, `headline`, `summary`, `skills`.

**`required_social_links`**: Which social link platforms are required. Any key from the `social_links` JSONB on the profile.

**`required_documents`**: Which document types are required. Enum: `CoverLetter`, `Certification`, `Portfolio`, `Reference`.

**`minimum_skills_count`**: Minimum number of skills the applicant must list. Default 1.

**`resume_required`**: Whether a resume upload is required. Default true.

**`ai_parsing_enabled`**: Whether to use AI-powered structured extraction when a resume is uploaded. When enabled, the Profiles module calls the AI Service to extract skills (with levels and years of experience), experience timeline, education details, and certifications into `ai_parsed_content` on `profiles.resumes`. When disabled (or AI Service unavailable), only the basic parser runs. Default true.

**`ai_parsing_provider`**: Which AI provider to use for resume parsing. Enum: `OpenAI`, `Anthropic`, `AzureOpenAI`. Default `OpenAI`.

---

### Screening Settings

```json
{
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
    },
    {
      "name": "Experience Level",
      "category": "Experience",
      "evaluation_method": "RangeMatch",
      "is_required": true,
      "weight": 30
    },
    {
      "name": "Education",
      "category": "Education",
      "evaluation_method": "ExactMatch",
      "is_required": false,
      "weight": 15
    },
    {
      "name": "Resume Quality",
      "category": "Custom",
      "evaluation_method": "SemanticSimilarity",
      "is_required": false,
      "weight": 15
    }
  ]
}
```

**`auto_advance_threshold`**: Minimum deterministic `overall_score` to auto-advance. If the job has `AfterScreening` questions, candidates advance to Assessment; if not, they advance to Shortlisted. Default 70.

**`auto_reject_threshold`**: Maximum deterministic `overall_score` to auto-reject. Default 30.

**`manual_review_policy`**: How to handle candidates between thresholds. Enum: `QueueForReview`, `AutoAdvanceAll`, `AutoRejectAll`, `NotifyAndHold`. Default `QueueForReview`.

**`ai_scoring_enabled`**: Tenant opt-in for AI analysis scoring. When ON (and system-wide gate is also ON), every candidate evaluation produces two scores side-by-side: deterministic (`criteria_score_breakdown`, `overall_score`) and AI analysis (`ai_criteria_score_breakdown`, `ai_overall_score`). Deterministic always drives routing; AI analysis is advisory for recruiter decisions. When OFF, only deterministic scoring runs — no degradation. Default false. See also: `SemanticSimilarity` evaluation method in the deterministic engine does basic keyword/overlap matching as fallback; AI engine does true semantic analysis.

**`candidate_transparency_enabled`**: Whether to generate candidate-facing evaluation feedback. When enabled, the Screening module calls the AI Service to produce a structured summary of the evaluation (criteria met, gaps, strengths) and stores it as `candidate_feedback` on the screening result. Exposed via a candidate-facing endpoint. Default false.

**`candidate_transparency_level`**: How much detail to include in candidate feedback. Enum: `None`, `Summary` (overall fit assessment + key strengths and gaps), `Detailed` (per-criteria breakdown with specific match/miss reasoning). Only applies when `candidate_transparency_enabled` is true. Default `Summary`.

**`default_evaluation_criteria`**: Template criteria copied to new job postings as `job_evaluation_criteria` rows. Recruiter can customize per job. Serves as the tenant-level base until a proper `criteria_templates` table is added. Each entry has the same shape as `recruitment.job_evaluation_criteria` — name, category, evaluation_method, is_required, weight. Configuration JSONB (category-specific shapes) is added per-job by the recruiter.

---

### Matching Settings

```json
{
  "screening_weight": 100,
  "assessment_weight": 0,
  "auto_generate_shortlist": true,
  "shortlist_size": 10
}
```

**`screening_weight` / `assessment_weight`**: How much each component contributes to the final compatibility score. Must sum to 100. Default: screening 100, assessment 0. When assessment questions are scored, tenants can adjust the split to factor assessment answers into the composite score (e.g., screening 60 / assessment 40).

**`auto_generate_shortlist`**: Whether to auto-generate a draft shortlist when enough candidates have full scores. Default true.

**`shortlist_size`**: How many top candidates to include in the auto-generated shortlist. Default 10.

---

### Assessment Settings

```json
{
  "enabled": true,
  "time_limit_minutes": 60,
  "allow_skip": true,
  "partial_completion_policy": "ScorePartial",
  "completion_policy": "AutoAdvance",
  "ai_assessment_questions_enabled": false
}
```

**`enabled`**: Whether the Assessment stage is available for jobs. When disabled, no AfterScreening questions can be created — all candidates are routed directly to Shortlisted after screening. Default true.

**`time_limit_minutes`**: Total time the candidate has to complete all assessment questions. Default 60.

**`allow_skip`**: Whether candidates can skip individual assessment questions. Default true.

**`partial_completion_policy`**: What to do if the assessment expires with partial responses. Enum: `ScorePartial` (score whatever was answered), `MarkIncomplete` (don't score, flag for review). Default `ScorePartial`.

**`completion_policy`**: What happens after assessment answers are scored. Enum: `AutoAdvance` (auto-advance to Shortlisted based on score), `QueueForReview` (recruiter reviews answers before advancing). Mirrors the pattern from Screening's `manual_review_policy`. Default `AutoAdvance`.

**`ai_assessment_questions_enabled`**: Tenant opt-in for AI-suggested assessment questions. When ON (and system-wide gate is also ON), a "Suggest assessment questions" button is available during job setup. The Recruitment module calls the AI Service to generate AfterScreening questions informed by the job description, title, and saved criteria. Recruiter reviews, edits, and saves suggestions. Same two-layer feature flag pattern as AI scoring. Default false.

> **Note:** When the AI Interview capability is implemented (deferred), additional AI-specific settings will be added here — `question_mix`, `allowed_response_types`, etc.

---

### Notification Settings

```json
{
  "notify_on_new_application": true,
  "notify_on_screening_complete": true,
  "notify_on_assessment_complete": true,
  "notify_on_manual_review_needed": true,
  "notify_on_offer_response": true,
  "notification_email": null
}
```

**`notification_email`**: Override email for notifications. NULL = send to the relevant recruiter/hiring manager. Set to a shared inbox if the tenant wants all notifications in one place.

These are tenant-level defaults. Per-user notification preferences can be added later as a separate `user_preferences` table if needed.

---

## Audit Details Format

The `details` JSONB column varies by action type. It captures the context needed to understand what happened without querying the affected entity (which may have changed since the audit entry was written).

**UserInvited:**

```json
{
  "invited_email": "jane@example.com",
  "invited_role": "Recruiter"
}
```

**SettingsUpdated:**

```json
{
  "field": "screening_settings",
  "previous": { "auto_advance_threshold": 70.0 },
  "updated": { "auto_advance_threshold": 65.0 }
}
```

**ScreeningReviewed:**

```json
{
  "application_id": "uuid",
  "outcome": "ManuallyAdvanced",
  "overall_score": 55.3,
  "review_notes": "Strong culture fit despite borderline score"
}
```

**OfferExtended:**

```json
{
  "application_id": "uuid",
  "salary": 120000.0,
  "salary_currency": "USD",
  "client_company": "Google"
}
```

The `details` structure is intentionally flexible — new action types can add new detail shapes without schema changes. Application code defines the structure per action type.

---

## Audit Coverage

Actions that should produce audit entries:

**Auth:**

- User invited, activated, deactivated
- Role changed
- OAuth provider linked/unlinked
- Password reset initiated

**Recruitment:**

- Job posting created, published, closed
- Application submitted (by applicant), withdrawn

**Screening:**

- Manual review decision (advanced or rejected)

**Matching:**

- Shortlist finalized

**HR Workflows:**

- Final interview scheduled, cancelled
- Interview recommendation recorded
- Job offer created, extended, accepted, declined, withdrawn

**Assessment:**

- Assessment questions submitted by candidate
- Assessment answers scored

**Admin:**

- Company settings updated (any field)
- Any platform admin action (tenant-level)

This list is enforced in application code, not in the database. The Admin module listens for domain events from other modules and writes audit entries. For actions within the Admin module itself (settings changes, user management), the audit write is inline.

---

## Design Decisions

**Single `company_settings` row with JSONB columns instead of a key-value table.** A key-value table (`setting_name`, `setting_value`) is flexible but untyped — you lose structure, validation, and the ability to read all settings in one query with predictable shape. JSONB columns grouped by domain (auth, screening, matching, etc.) give structure within flexibility. Each column has a defined schema enforced in application code, and adding new settings within a group is a JSONB change, not a migration. Adding a new group is a migration, but that's rare and intentional.

**JSONB settings columns instead of flat columns.** Settings are hierarchical and group-specific. Flat columns would mean 30+ columns on the settings table, many of which only make sense together (e.g., `auto_advance_threshold` is meaningless without `auto_reject_threshold`). JSONB groups keep related settings together and make it obvious which settings belong to which module.

**Denormalized actor fields on audit logs.** `actor_email` and `actor_role` are captured at action time, not looked up via FK. Users can change email, change role, or be deleted — the audit record must show who they were when they acted, not who they are now. This is standard audit log practice.

**No FK on `actor_id`.** If a user is deleted, the audit trail must survive. A FK would either prevent deletion (bad UX) or cascade-delete audit entries (data loss). Plain UUID preserves the record.

**Append-only audit logs, no updates or deletes.** Audit integrity requires immutability. Rows are written once and never modified. If an audit entry is wrong, a correction entry is written — the original is never changed. Cleanup (if needed) is a retention policy that bulk-deletes entries older than a configurable period, not individual deletions.

**`performed_at` separate from `created_at`.** In most cases they're identical. But if audit entries are written asynchronously (queued from domain events), `performed_at` is when the action actually happened and `created_at` is when the log row was written. The distinction matters for event-driven audit logging with any processing delay.

**`ip_address` and `user_agent` for security auditing.** Tracks where actions came from. Useful for investigating suspicious activity ("someone deactivated all users from an unfamiliar IP") and for compliance requirements that mandate access logging.

**Action as a string enum, not a separate actions table.** New action types are added in code, not in the database. A lookup table would add a join for no benefit — the action names are self-documenting and rarely queried in aggregate. The `ix_audit_logs_action` index supports filtering by action type when needed.

**Settings seeded with defaults during provisioning.** When a new tenant is provisioned, a `company_settings` row is created with sensible defaults for all JSONB columns. The tenant admin customizes from there. This means every tenant always has a complete settings row — no NULL checks or "use platform default if tenant hasn't configured this" logic scattered across modules.

**Notification settings at tenant level, not per-user.** Per-user notification preferences are a common future need, but the tenant-level defaults cover the initial use case. A `user_notification_preferences` table can be added later, with the tenant settings serving as the fallback when a user hasn't configured their own.

**No company name, logo, or website on settings — catalog is the source of truth.** The catalog DB already stores `tenants.name` and `tenant_brandings` (logo, colors, favicon). Duplicating these in the tenant DB creates two sources of truth and sync issues. When any module needs the tenant's display name or branding, it reads from the cached catalog data (already in Redis from tenant resolution). `company_settings` only stores operational configuration that the catalog doesn't know about — timezone, currency, module-specific behavior.
