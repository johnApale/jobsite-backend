# Screening Module — Database Design

Automated first-pass evaluation of applications. When an application is submitted, the Screening module parses the resume, matches skills against job requirements, and scores the candidate. Rather than a binary pass/fail, screening produces a match strength indicator and an overall score — the tenant's configuration then determines what happens next.

Listens for `ApplicationSubmittedEvent` from Recruitment. On completion, publishes `CvScreeningCompletedEvent` (consumed by Matching). Depending on the tenant's auto-advance configuration, may also publish `CandidateReadyForInterviewEvent` (integration event via message broker → AI Interview Service) or flag the application for manual review.

---

## Tables

### screening_results

One screening result per application. Created when the Screening module begins evaluating an application, updated as each phase of screening completes. This is the full evaluation record — scores, match strength, skill breakdown, extracted resume data, and the routing outcome.

One-to-one with `recruitment.applications` using a shared primary key (`application_id` is both PK and FK). Same pattern as `tenant_brandings` and `applicant_profiles`.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| application_id | uuid | PK, FK → recruitment.applications.id | Shared key — one screening result per application |
| status | varchar(20) | NOT NULL | Enum: `Pending`, `InProgress`, `Completed`, `Failed` |
| overall_score | decimal(5,2) | nullable | Final weighted score (0.00–100.00). Set on completion. NULL while in progress |
| skill_match_score | decimal(5,2) | nullable | How well the applicant's skills match job requirements (0.00–100.00) |
| experience_match_score | decimal(5,2) | nullable | Per-skill years vs job requirements scoring (0.00–100.00) |
| resume_quality_score | decimal(5,2) | nullable | Resume completeness, formatting, relevance (0.00–100.00) |
| match_strength | varchar(20) | nullable | Enum: `Strong`, `Good`, `Moderate`, `Weak`. Derived from `overall_score` ranges. Human-readable label for recruiters. NULL while in progress |
| outcome | varchar(20) | nullable | Enum: `AutoAdvanced`, `AutoRejected`, `ManualReview`, `ManuallyAdvanced`, `ManuallyRejected`. What actually happened to this application after scoring. See Outcome section below |
| auto_advance_threshold | decimal(5,2) | NOT NULL | Tenant's auto-advance threshold at evaluation time. Captured so historical results stay interpretable |
| auto_reject_threshold | decimal(5,2) | NOT NULL | Tenant's auto-reject threshold at evaluation time. Captured for same reason |
| reviewed_by | uuid | nullable, FK → auth.users.id | The recruiter/manager who manually reviewed this result. NULL for auto-advanced and auto-rejected applications |
| reviewed_at | timestamp | nullable | When the manual review decision was made |
| review_notes | text | nullable | Optional notes from the reviewer explaining their decision |
| skill_match_details | jsonb | nullable | Per-skill breakdown of the match. See Skill Match Details Format below |
| failure_reason | varchar(500) | nullable | If `status` = `Failed` (processing error, not candidate evaluation): what went wrong (e.g., "Resume parsing failed", "Unsupported file format") |
| started_at | timestamp | nullable | When screening began processing |
| completed_at | timestamp | nullable | When screening finished (scored, not necessarily routed) |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_screening_results_status | status | Non-unique | Monitor in-progress screenings, find failed ones for retry |
| ix_screening_results_match_strength | match_strength | Non-unique | Filter by match strength for recruiter dashboards |
| ix_screening_results_outcome | outcome | Non-unique | Filter by routing outcome (especially `ManualReview` — the recruiter's queue) |
| ix_screening_results_overall_score | overall_score | Non-unique | Ranking candidates by score across a job's applications |

No index on `application_id` — it's the primary key.

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS screening;
```

All Screening module tables live under the `screening` schema. Matching reads screening scores through a shared interface — not by querying this table directly.

---

## Relationships

```
recruitment.applications ||--o| screening_results : "has (optional, one-to-one)"
auth.users ||--o{ screening_results : "reviewed_by (manual reviewer)"
```

The FK references `recruitment.applications` across schemas. The screening result is an extension of the application — it only exists because an application was submitted. The shared primary key enforces one-to-one at the database level.

---

## Match Strength

Derived from `overall_score` ranges. The exact ranges are defined in application code, not in the database. Default ranges:

| Match Strength | Score Range | Meaning |
|----------------|-------------|---------|
| **Strong** | 80–100 | Excellent fit. Exceeds most requirements |
| **Good** | 60–79 | Solid fit. Meets core requirements |
| **Moderate** | 40–59 | Partial fit. Meets some requirements, gaps in others |
| **Weak** | 0–39 | Poor fit. Missing most requirements |

Match strength is stored as a denormalized label for quick filtering and display. The `overall_score` is the precise value for ranking and threshold comparisons.

---

## Three-Tier Routing Model

After screening completes, the application is routed based on the tenant's configured thresholds:

```
100 ───────────────
                    → Auto-advance: application moves to AI Interview automatically
 70 ─────────────── auto_advance_threshold (tenant-configurable, default 70)
                    → Manual Review: recruiter decides
 30 ─────────────── auto_reject_threshold (tenant-configurable, default 30)
                    → Auto-reject: application is rejected automatically
  0 ───────────────
```

**Above auto-advance threshold:** Application automatically moves to `AiInterview`. `CandidateReadyForInterviewEvent` published. `outcome` = `AutoAdvanced`.

**Below auto-reject threshold:** Application automatically moves to `Rejected`. `outcome` = `AutoRejected`.

**Between thresholds (the review zone):** What happens here depends on the tenant's `manual_review_policy` in CompanySettings:
- `QueueForReview` — application sits in `ManualReview` outcome, waiting for a recruiter to act. Application status stays at `Screening` until the recruiter advances or rejects.
- `AutoAdvanceAll` — treat the middle zone like the top zone. Everything above the auto-reject threshold advances automatically.
- `AutoRejectAll` — treat the middle zone like the bottom zone. Only candidates above the auto-advance threshold proceed.
- `NotifyAndHold` — same as `QueueForReview` but triggers a notification to the assigned recruiter/hiring manager.

---

## Tenant Screening Configuration

Stored in the Admin module's `CompanySettings` table (not in the Screening schema). The Screening module reads this at evaluation time and captures the thresholds on the result row.

```json
{
  "screening": {
    "auto_advance_threshold": 70.00,
    "auto_reject_threshold": 30.00,
    "manual_review_policy": "QueueForReview",
    "score_weights": {
      "skill_match": 50,
      "experience_match": 30,
      "resume_quality": 20
    }
  }
}
```

**`auto_advance_threshold`**: Minimum `overall_score` to auto-advance to AI Interview. Default: 70.00.

**`auto_reject_threshold`**: Maximum `overall_score` to auto-reject. Default: 30.00.

**`manual_review_policy`**: How to handle candidates in the review zone. Enum: `QueueForReview`, `AutoAdvanceAll`, `AutoRejectAll`, `NotifyAndHold`. Default: `QueueForReview`.

**`score_weights`**: Relative weights for the sub-scores (must sum to 100). Default: skill_match 50, experience_match 30, resume_quality 20. Allows tenants to prioritize what matters most to them.

---

## Outcome

The `outcome` column records what actually happened to the application after scoring. This is distinct from the screening `status` (which tracks the scoring process) and from the application `status` (which tracks the pipeline stage).

- **AutoAdvanced**: Score was above `auto_advance_threshold`. Application moved to AI Interview automatically.
- **AutoRejected**: Score was below `auto_reject_threshold`. Application rejected automatically.
- **ManualReview**: Score was between thresholds and tenant policy is `QueueForReview` or `NotifyAndHold`. Waiting for recruiter action.
- **ManuallyAdvanced**: Recruiter reviewed and decided to advance. `reviewed_by`, `reviewed_at`, and optionally `review_notes` are set.
- **ManuallyRejected**: Recruiter reviewed and decided to reject. Same review fields set.

The outcome is NULL while screening is in progress and set when scoring completes (for auto outcomes) or when a recruiter acts (for manual outcomes).

---

## Screening Status vs Application Status

Two different things:

**`screening_results.status`** tracks the screening *process* itself:
- **Pending**: Screening result row created, waiting to be picked up by the processor
- **InProgress**: CV parsing and scoring underway
- **Completed**: Scoring finished. `match_strength` and `overall_score` are set. Outcome determined
- **Failed**: Processing error — the screening couldn't complete (bad file format, service timeout, etc.). Not a candidate evaluation. Can be retried

**`recruitment.applications.status`** tracks the candidate's pipeline stage:
- When outcome is `AutoAdvanced` or `ManuallyAdvanced` → application status moves to `AiInterview`
- When outcome is `AutoRejected` or `ManuallyRejected` → application status moves to `Rejected`
- When outcome is `ManualReview` → application status stays at `Screening` until the recruiter acts

---

## Skill Match Details Format

Per-skill breakdown of how the applicant matched against the job's requirements. Stored as JSONB for flexibility and because Matching reads this as a complete object, not individual rows.

```json
[
  {
    "skill": "C#",
    "required": true,
    "required_level": "Intermediate",
    "required_years": 5,
    "applicant_level": "Advanced",
    "applicant_years": 7,
    "source": "Profile",
    "match": "Exceeds"
  },
  {
    "skill": "PostgreSQL",
    "required": true,
    "required_level": "Beginner",
    "required_years": 2,
    "applicant_level": "Intermediate",
    "applicant_years": 3,
    "source": "Profile",
    "match": "Exceeds"
  },
  {
    "skill": "Kubernetes",
    "required": true,
    "required_level": "Beginner",
    "required_years": 1,
    "applicant_level": null,
    "applicant_years": null,
    "source": null,
    "match": "Missing"
  },
  {
    "skill": "React",
    "required": false,
    "required_level": null,
    "required_years": null,
    "applicant_level": "Beginner",
    "applicant_years": 1,
    "source": "Resume",
    "match": "Bonus"
  }
]
```

**`skill`**: The skill name from the job requirements.

**`required`**: Whether this was a required skill or nice-to-have.

**`required_level` / `required_years`**: What the job asked for. NULL for nice-to-have skills without minimums.

**`applicant_level` / `applicant_years`**: What the applicant has. NULL if the skill is missing entirely.

**`source`**: Where the applicant's skill data came from — `Profile` (self-reported in applicant_profiles.skills), `Resume` (extracted by the CV parser), or `Both`. Useful for understanding data quality and discrepancies.

**`match`**: Enum — `Exceeds` (meets or beats requirements), `Partial` (has the skill but below required level/years), `Missing` (doesn't have it at all), `Bonus` (not required but the applicant has it).

---

## Extracted Skills vs Profile Skills

The Screening module has two sources of skill data for a candidate:

1. **Profile skills** — self-reported by the applicant in `profiles.applicant_profiles.skills`
2. **Extracted skills** — parsed from the resume by the Profiles module's background parser, stored in `profiles.resumes.extracted_skills`

These may differ. An applicant might list "C#" in their profile but their resume says "5 years of .NET development" without explicitly naming C#. Or the resume might mention skills the applicant forgot to add to their profile.

Both data sources are pre-computed and available when Screening runs — no parsing happens during screening. The scoring algorithm in application code decides how to reconcile the two sources (profile skills, extracted skills, or a weighted combination).

---

## Scoring Pipeline

```
1. ApplicationSubmittedEvent received
2. screening_results row created (status = Pending)
3. Processor picks up the job:
   a. Status → InProgress, started_at = now
   b. Fetch tenant's screening configuration from Admin (CompanySettings)
   c. Fetch the application's resume record via resume_id (from profiles.resumes)
      → parsed_text and extracted_skills are already available (parsed on upload)
   d. Fetch applicant's profile skills from Profiles module
   e. Fetch job's required_skills and nice_to_have_skills from Recruitment module
   f. Run skill matching → compute skill_match_score, build skill_match_details
   g. Run experience matching → compute experience_match_score
   h. Run resume quality evaluation → compute resume_quality_score
   i. Compute overall_score using tenant's score_weights
   j. Derive match_strength from overall_score ranges
   k. Capture auto_advance_threshold and auto_reject_threshold on the row
4. Status → Completed, completed_at = now
5. Determine routing:
   a. Score >= auto_advance_threshold → outcome = AutoAdvanced
      → Update application status to AiInterview
      → Publish CandidateReadyForInterviewEvent (integration event → broker → AI Interview)
   b. Score <= auto_reject_threshold → outcome = AutoRejected
      → Update application status to Rejected
   c. Score between thresholds → apply tenant's manual_review_policy:
      → QueueForReview / NotifyAndHold: outcome = ManualReview (application stays at Screening)
      → AutoAdvanceAll: outcome = AutoAdvanced (same as 5a)
      → AutoRejectAll: outcome = AutoRejected (same as 5b)
6. Publish CvScreeningCompletedEvent (domain event → Matching) in all cases
```

---

## Manual Review Flow

```
1. Screening completes with outcome = ManualReview
2. Application appears in recruiter's "Needs Review" queue
   (filtered by: screening_results.outcome = 'ManualReview')
3. Recruiter reviews screening scores, skill match details, match strength
4. Recruiter decides:
   a. Advance → outcome updated to ManuallyAdvanced
      → reviewed_by, reviewed_at, review_notes set
      → Application status → AiInterview
      → CandidateReadyForInterviewEvent published
   b. Reject → outcome updated to ManuallyRejected
      → reviewed_by, reviewed_at, review_notes set
      → Application status → Rejected
```

---

## Error Handling & Retries

If screening fails (resume download error, parser crash, AI service timeout), the row is marked `status = Failed` with a `failure_reason`. This is distinct from a low score or auto-rejection — processing failures are technical errors, not evaluations.

Failed screenings can be retried. The retry mechanism picks up rows where `status = Failed` and reprocesses them. The `failure_reason` helps with debugging and deciding whether to retry (transient network error → retry, unsupported file format → don't retry, notify the applicant).

---

## Design Decisions

**Match strength instead of pass/fail.** Binary gates are too rigid for hiring. A candidate scoring 55% might be a great fit for a hard-to-fill role but would be auto-rejected by a pass/fail system. Match strength gives recruiters a quick read (`Strong`, `Good`, `Moderate`, `Weak`) while the `overall_score` provides precision for ranking. The tenant's thresholds control automation; the recruiter's judgment handles the gray area.

**Three-tier routing model.** Top tier auto-advances (clear strong matches shouldn't wait for human review), bottom tier auto-rejects (obvious mismatches shouldn't waste recruiter time), middle tier gets human judgment. The thresholds are tenant-configurable because every company has different standards and applicant volumes.

**Tenant-configurable review policy.** Different tenants have different workflows. A high-volume staffing agency might use `AutoAdvanceAll` to keep the pipeline moving. A boutique firm hiring for senior roles might use `QueueForReview` with a narrow auto-advance threshold so humans see most candidates. The policy is a CompanySettings value, not a code change.

**Thresholds captured on the result row.** Tenants can change their thresholds at any time. Storing the thresholds that were active at evaluation time means you can always reconstruct why a particular routing decision was made. "This candidate was auto-rejected because they scored 28 and the auto-reject threshold was 30 at the time" — not ambiguous even if the tenant later changes the threshold to 20.

**`outcome` separate from screening `status` and application `status`.** Three things are tracked independently: the screening process (Pending → InProgress → Completed), the routing decision (AutoAdvanced / ManualReview / etc.), and the application pipeline stage (Screening → AiInterview / Rejected). Conflating them would make queries confusing and state transitions error-prone.

**Manual review fields on the screening result, not a separate table.** `reviewed_by`, `reviewed_at`, and `review_notes` are nullable columns on `screening_results`. A separate `screening_reviews` table would be warranted if multiple people could review the same screening result — but this is a single recruiter making a single advance/reject decision. One set of fields is sufficient.

**Shared primary key with `recruitment.applications`.** One screening result per application, enforced at the database level. The screening result is an extension of the application, not an independent entity. Same pattern used throughout the project.

**Separate sub-scores, not just an overall score.** `skill_match_score`, `experience_match_score`, and `resume_quality_score` are stored individually so that: Matching can weight them differently when ranking, dashboards can show where candidates are strong/weak, and the scoring weights can be tuned without re-screening every application.

**Resume parsing lives in Profiles, not Screening.** Resumes are parsed once on upload by the Profiles module's background parser. The parsed text and extracted skills are stored on the `profiles.resumes` record. Screening reads this pre-parsed data via the application's `resume_id` FK — no downloading, no re-parsing, no duplicate storage. Ten applications with the same resume all read from one parsed copy.

**Two skill data sources reconciled in code.** Profile skills (self-reported) and resume extracted skills (parser-inferred) may differ. Both are available to the Screening algorithm. The `skill_match_details` JSONB records which source each data point came from, giving visibility into data quality and discrepancies.

**`Failed` status for processing errors, not for low-scoring candidates.** A processing failure means "we couldn't evaluate this person" — it should be retried. A low score with `AutoRejected` outcome means "we evaluated them and they didn't meet the bar." Conflating these would make retry logic dangerous.

**JSONB for skill match details.** The breakdown is always read and written as a complete object — never queried field-by-field across screening results. JSONB keeps the schema flat and avoids a `screening_skill_matches` join table that would add complexity for no queryability benefit.

**Cross-schema FK to `recruitment.applications`.** Screening is tightly coupled to Recruitment by design — it can't exist without an application. The FK makes this dependency explicit and prevents orphaned screening results.

**No index on `skill_match_details`.** This JSONB column is large and read per-record, not queried across records. A GIN index would be expensive to maintain and wouldn't serve any realistic query pattern.
