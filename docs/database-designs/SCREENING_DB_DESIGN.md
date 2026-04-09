# Screening Module — Database Design

Criteria-driven screening and evaluation engine. When an application is submitted, the Screening module evaluates the applicant against the job's structured evaluation criteria using pre-parsed resume data and profile information. Rather than a binary pass/fail, screening produces a match strength indicator and an overall score — the tenant's configuration then determines what happens next.

The module supports a **dual scoring engine**: a deterministic engine (always runs, zero-cost, drives all routing and automation) and an optional AI analysis engine (feature-flagged, calls the AI Service for richer per-criterion reasoning). Both scores are stored side-by-side for recruiter review.

The module also manages the **Assessment stage** — an optional phase where candidates answer recruiter-defined `AfterScreening` questions before being shortlisted.

Listens for `ApplicationSubmittedEvent` from Recruitment. On completion, publishes `CvScreeningCompletedEvent` (consumed by Matching). When assessment questions are present and answered, publishes `AssessmentCompletedEvent` (consumed by Matching).

---

## Tables

### screening_results

One screening result per application. Created when the Screening module begins evaluating an application, updated as each phase of screening completes. This is the full evaluation record — deterministic scores, optional AI analysis scores, criteria breakdowns, question scores, and the routing outcome.

One-to-one with `recruitment.applications` using a shared primary key (`application_id` is both PK and FK). Same pattern as `tenant_brandings` and `applicant_profiles`.

| Column                      | Type         | Constraints                           | Description                                                                                                                                                                                                                                                 |
| --------------------------- | ------------ | ------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| application_id              | uuid         | PK, ref → recruitment.applications.id | Shared key — one screening result per application                                                                                                                                                                                                           |
| status                      | varchar(20)  | NOT NULL                              | Enum: `Pending`, `InProgress`, `Completed`, `Failed`                                                                                                                                                                                                        |
| overall_score               | decimal(5,2) | nullable                              | Final weighted deterministic score (0.00–100.00). Drives all routing and automation. Set on completion. NULL while in progress                                                                                                                              |
| match_strength              | varchar(20)  | nullable                              | Enum: `Strong`, `Good`, `Moderate`, `Weak`. Derived from `overall_score` ranges. Human-readable label for recruiters. NULL while in progress                                                                                                                |
| outcome                     | varchar(20)  | nullable                              | Enum: `AutoAdvanced`, `AutoRejected`, `ManualReview`, `ManuallyAdvanced`, `ManuallyRejected`. What happened to this application after scoring. See Outcome section below                                                                                    |
| criteria_score_breakdown    | jsonb        | nullable                              | Per-criterion **deterministic** score details. Always populated on completion. See Criteria Score Breakdown Format below                                                                                                                                    |
| ai_criteria_score_breakdown | jsonb        | nullable                              | Per-criterion **AI analysis** score details. Same structure as deterministic but with richer AI-generated reasoning. NULL if AI scoring is disabled or AI Service is unavailable                                                                            |
| ai_overall_score            | decimal(5,2) | nullable                              | AI's overall score for the candidate (0.00–100.00). Stored alongside deterministic `overall_score` for comparison. NULL if AI scoring is disabled or unavailable                                                                                            |
| question_score_breakdown    | jsonb        | nullable                              | Per-question scoring. See Question Score Breakdown Format below. NULL if the job has no `AtApplication` questions                                                                                                                                           |
| assessment_score            | decimal(5,2) | nullable                              | Separate weighted score from assessment (`AfterScreening`) question answers (0.00–100.00). NULL until assessment answers are scored. Final pipeline score = weighted combination of `overall_score` + `assessment_score` (weights from `matching_settings`) |
| candidate_feedback          | jsonb        | nullable                              | Candidate-facing transparency data. Structured summary of evaluation (criteria met, gaps, strengths). Only populated if tenant has `candidate_transparency_enabled = true`. See Candidate Feedback Format below                                             |
| auto_advance_threshold      | decimal(5,2) | NOT NULL                              | Tenant's auto-advance threshold at evaluation time. Captured so historical results stay interpretable                                                                                                                                                       |
| auto_reject_threshold       | decimal(5,2) | NOT NULL                              | Tenant's auto-reject threshold at evaluation time. Captured for same reason                                                                                                                                                                                 |
| reviewed_by                 | uuid         | nullable, ref → auth.users.id         | The recruiter/manager who manually reviewed this result. NULL for auto-advanced and auto-rejected applications                                                                                                                                              |
| reviewed_at                 | timestamp    | nullable                              | When the manual review decision was made                                                                                                                                                                                                                    |
| review_notes                | text         | nullable                              | Optional notes from the reviewer explaining their decision                                                                                                                                                                                                  |
| failure_reason              | varchar(500) | nullable                              | If `status` = `Failed` (processing error, not candidate evaluation): what went wrong (e.g., "Resume parsing failed", "AI Service timeout")                                                                                                                  |
| started_at                  | timestamp    | nullable                              | When screening began processing                                                                                                                                                                                                                             |
| completed_at                | timestamp    | nullable                              | When screening finished (scored, not necessarily routed)                                                                                                                                                                                                    |
| created_at                  | timestamp    | NOT NULL                              |                                                                                                                                                                                                                                                             |
| updated_at                  | timestamp    | NOT NULL                              | Auto-set on modification                                                                                                                                                                                                                                    |

**Indexes:**

| Name                                | Columns        | Type       | Purpose                                                                       |
| ----------------------------------- | -------------- | ---------- | ----------------------------------------------------------------------------- |
| ix_screening_results_status         | status         | Non-unique | Monitor in-progress screenings, find failed ones for retry                    |
| ix_screening_results_match_strength | match_strength | Non-unique | Filter by match strength for recruiter dashboards                             |
| ix_screening_results_outcome        | outcome        | Non-unique | Filter by routing outcome (especially `ManualReview` — the recruiter's queue) |
| ix_screening_results_overall_score  | overall_score  | Non-unique | Ranking candidates by score across a job's applications                       |

No index on `application_id` — it's the primary key.

---

### screening_question_responses

Candidate answers to screening questions, stored in the screening schema. This table handles answers for both `AtApplication` and `AfterScreening` questions. Answers are written when the candidate submits them; scoring happens separately (deterministic for MultipleChoice/YesNo, AI Service for FreeText).

| Column          | Type         | Constraints                                                    | Description                                                                                                                            |
| --------------- | ------------ | -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| id              | uuid         | PK                                                             |                                                                                                                                        |
| application_id  | uuid         | NOT NULL, ref → recruitment.applications.id                    | The application this answer belongs to                                                                                                 |
| question_id     | uuid         | NOT NULL, logical ref → recruitment.job_screening_questions.id | The question being answered. Cross-schema reference to Recruitment (no DB-level FK)                                                    |
| response_text   | text         | nullable                                                       | Free-text answer for `FreeText` questions. NULL for `MultipleChoice`/`YesNo`                                                           |
| response_data   | jsonb        | nullable                                                       | Structured answer data. For `MultipleChoice`: `{ "selected_options": [0, 2] }`. For `YesNo`: `{ "answer": true }`. NULL for `FreeText` |
| score           | decimal(5,2) | nullable                                                       | Score for this answer (0.00–100.00). Set after scoring. NULL until scored                                                              |
| score_result    | varchar(20)  | nullable                                                       | Enum: `MeetsRequirement`, `PartialMatch`, `Missing`. Quick label for the score. NULL until scored                                      |
| score_reasoning | text         | nullable                                                       | Explanation of the score. For AI-scored FreeText: the AI's reasoning. For deterministic: rule description. NULL until scored           |
| submitted_at    | timestamp    | NOT NULL                                                       | When the candidate submitted this answer                                                                                               |
| scored_at       | timestamp    | nullable                                                       | When this answer was scored. NULL until scored                                                                                         |
| created_at      | timestamp    | NOT NULL                                                       |                                                                                                                                        |

**Constraints:**

| Name                               | Columns                     | Type   | Purpose                                 |
| ---------------------------------- | --------------------------- | ------ | --------------------------------------- |
| uq_question_responses_app_question | application_id, question_id | Unique | One answer per question per application |

**Indexes:**

| Name                                 | Columns        | Type       | Purpose                            |
| ------------------------------------ | -------------- | ---------- | ---------------------------------- |
| ix_question_responses_application_id | application_id | Non-unique | "All answers for this application" |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS screening;
```

All Screening module tables live under the `screening` schema. Matching reads screening scores through a shared interface — not by querying this table directly.

---

## Relationships

```
recruitment.applications           ||--o| screening_results              : "has (optional, one-to-one)"
recruitment.applications           ||--o{ screening_question_responses   : "has many"
recruitment.job_screening_questions ||--o{ screening_question_responses   : "has many (cross-schema reference)"
auth.users                         ||--o{ screening_results              : "reviewed_by (manual reviewer)"
```

The `application_id` column references `recruitment.applications` across schemas as a logical reference (no DB-level FK constraint). The screening result is an extension of the application — it only exists because an application was submitted. The shared primary key enforces one-to-one at the database level. Integrity is guaranteed by domain events: the `ApplicationSubmittedEvent` triggers screening result creation, so the source application always exists.

`screening_question_responses.question_id` references `recruitment.job_screening_questions` across schemas — also a logical reference with no FK constraint. Integrity is enforced at the application layer via the `IJobScreeningQuestionsReader` SharedKernel interface.

---

## Match Strength

Derived from `overall_score` ranges. The exact ranges are defined in application code, not in the database. Default ranges:

| Match Strength | Score Range | Meaning                                              |
| -------------- | ----------- | ---------------------------------------------------- |
| **Strong**     | 80–100      | Excellent fit. Exceeds most requirements             |
| **Good**       | 60–79       | Solid fit. Meets core requirements                   |
| **Moderate**   | 40–59       | Partial fit. Meets some requirements, gaps in others |
| **Weak**       | 0–39        | Poor fit. Missing most requirements                  |

Match strength is stored as a denormalized label for quick filtering and display. The `overall_score` is the precise value for ranking and threshold comparisons.

---

## Three-Tier Routing Model

After screening completes, the application is routed based on the tenant's configured thresholds:

```
100 ───────────────
                    → Auto-advance: Assessment (if AfterScreening questions exist)
                                    or Shortlisted (if no AfterScreening questions)
 70 ─────────────── auto_advance_threshold (tenant-configurable, default 70)
                    → Manual Review: recruiter decides
 30 ─────────────── auto_reject_threshold (tenant-configurable, default 30)
                    → Auto-reject: application is rejected automatically
  0 ───────────────
```

**Above auto-advance threshold:** Application automatically moves to `Assessment` (if the job has `AfterScreening` questions) or `Shortlisted` (if no `AfterScreening` questions). `outcome` = `AutoAdvanced`.

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

**`auto_advance_threshold`**: Minimum `overall_score` to auto-advance. Default: 70.00.

**`auto_reject_threshold`**: Maximum `overall_score` to auto-reject. Default: 30.00.

**`manual_review_policy`**: How to handle candidates in the review zone. Enum: `QueueForReview`, `AutoAdvanceAll`, `AutoRejectAll`, `NotifyAndHold`. Default: `QueueForReview`.

**`ai_scoring_enabled`**: Tenant opt-in for AI analysis scoring. When `true` (and the system-wide gate is ON), AI scoring runs alongside deterministic scoring. Default: `false`.

**`candidate_transparency_enabled`**: Whether to generate candidate-facing feedback. Default: `false`.

**`candidate_transparency_level`**: Detail level for candidate feedback. Enum: `None`, `Summary`, `Detailed`. Default: `Summary`.

**`default_evaluation_criteria`**: Template criteria copied to new job postings. Recruiters customize per job. Weights should sum to 100.

---

## Outcome

The `outcome` column records what actually happened to the application after scoring. This is distinct from the screening `status` (which tracks the scoring process) and from the application `status` (which tracks the pipeline stage).

- **AutoAdvanced**: Score was above `auto_advance_threshold`. Application moved to Assessment (if `AfterScreening` questions exist) or Shortlisted automatically.
- **AutoRejected**: Score was below `auto_reject_threshold`. Application rejected automatically.
- **ManualReview**: Score was between thresholds and tenant policy is `QueueForReview` or `NotifyAndHold`. Waiting for recruiter action.
- **ManuallyAdvanced**: Recruiter reviewed and decided to advance. `reviewed_by`, `reviewed_at`, and optionally `review_notes` are set.
- **ManuallyRejected**: Recruiter reviewed and decided to reject. Same review fields set.

The outcome is NULL while screening is in progress and set when scoring completes (for auto outcomes) or when a recruiter acts (for manual outcomes).

---

## Screening Status vs Application Status

Two different things:

**`screening_results.status`** tracks the screening _process_ itself:

- **Pending**: Screening result row created, waiting to be picked up by the processor
- **InProgress**: Criteria evaluation and scoring underway
- **Completed**: Scoring finished. `match_strength` and `overall_score` are set. Outcome determined
- **Failed**: Processing error — the screening couldn't complete (bad file format, service timeout, etc.). Not a candidate evaluation. Can be retried

**`recruitment.applications.status`** tracks the candidate's pipeline stage:

- When outcome is `AutoAdvanced` or `ManuallyAdvanced`:
  - If job has `AfterScreening` questions → application status moves to `Assessment`
  - If no `AfterScreening` questions → application status moves to `Shortlisted`
- When outcome is `AutoRejected` or `ManuallyRejected` → application status moves to `Rejected`
- When outcome is `ManualReview` → application status stays at `Screening` until the recruiter acts

---

## Criteria Score Breakdown Format

Per-criterion breakdown stored in `criteria_score_breakdown` (deterministic) and `ai_criteria_score_breakdown` (AI analysis). Both use the same structure. The deterministic version drives all routing; the AI version provides richer reasoning for recruiter review.

```json
[
  {
    "criterion_id": "uuid",
    "criterion_name": "C# Proficiency",
    "category": "Skill",
    "weight": 25.0,
    "score": 85.0,
    "result": "MeetsRequirement",
    "reasoning": "Applicant has 7 years of Advanced C# (required: 5 years Intermediate)"
  },
  {
    "criterion_id": "uuid",
    "criterion_name": "5+ Years Backend Experience",
    "category": "Experience",
    "weight": 30.0,
    "score": 60.0,
    "result": "PartialMatch",
    "reasoning": "3 years detected; 5 years required"
  },
  {
    "criterion_id": "uuid",
    "criterion_name": "AWS Certification",
    "category": "Certification",
    "weight": 15.0,
    "score": 0.0,
    "result": "Missing",
    "reasoning": "No AWS Solutions Architect certification found in profile or resume"
  }
]
```

**`criterion_id`**: References `recruitment.job_evaluation_criteria.id` for traceability.

**`criterion_name`**: Denormalized name for display without joining.

**`category`**: The criterion category (Skill, Experience, etc.).

**`weight`**: The criterion's weight. Stored for snapshot consistency — weights may change after scoring.

**`score`**: Score for this criterion (0.00–100.00).

**`result`**: Quick classification. Enum: `MeetsRequirement`, `PartialMatch`, `Missing`.

**`reasoning`**: Human-readable explanation. Deterministic version: rule-based description. AI version: richer LLM-generated analysis.

---

## Question Score Breakdown Format

Per-question scoring stored in `question_score_breakdown`. Populated when `AtApplication` questions are scored during screening.

```json
[
  {
    "question_id": "uuid",
    "score": 90.0,
    "reasoning": "Candidate selected correct options (A, C)"
  },
  {
    "question_id": "uuid",
    "score": 75.0,
    "reasoning": "Response covers scalability and caching but lacks specific examples"
  }
]
```

---

## Candidate Feedback Format

Structured transparency data for candidates. Only populated when the tenant enables candidate transparency. Generated by the AI Service's feedback endpoint.

```json
{
  "level": "Summary",
  "overall_fit": "Good",
  "strengths": [
    "Strong technical skills matching the core requirements",
    "Relevant industry experience in backend development"
  ],
  "gaps": [
    "Missing required AWS certification",
    "Below the preferred experience threshold for this role"
  ],
  "recommendation": "Consider obtaining the AWS Solutions Architect certification to strengthen your profile for similar roles"
}
```

**`level`**: The transparency level this feedback was generated at (`Summary` or `Detailed`).

For `Detailed` level, each criterion's breakdown is included (criteria met, partial matches, missing items with specific guidance).

---

## Dual Scoring Engine

The Screening module implements two scoring engines that can run side-by-side:

### Deterministic Scoring Engine (always runs)

Local, zero-cost, rule-based scoring. Evaluates each criterion using its configured `evaluation_method`:

- **ExactMatch**: Binary — has it or doesn't. Checks profile skills, resume extracted skills, certifications, education.
- **RangeMatch**: Proportional — compares numeric values. "3 years when 5 required" scores proportionally.
- **SemanticSimilarity**: Keyword matching and text overlap as a basic fallback. Checks for keyword presence in resume text and profile data.

Produces: `criteria_score_breakdown`, `overall_score`, `match_strength`.

### AI Analysis Scoring Engine (feature-flagged)

Calls the AI Service's `/api/v1/ai/screening/evaluate` endpoint. Requires **both** the system-wide gate (in `appsettings.json`) **and** the tenant's `ai_scoring_enabled` flag to be ON. When either is OFF, AI scoring is skipped — the fields stay NULL with no degradation.

Provides richer per-criterion analysis with LLM-generated reasoning. `SemanticSimilarity` criteria get true semantic understanding instead of keyword matching.

Produces: `ai_criteria_score_breakdown`, `ai_overall_score`.

### Why Two Engines?

- **Deterministic drives automation** — routing decisions (auto-advance, auto-reject, manual review) use only the deterministic `overall_score`. This ensures consistent, predictable, auditable pipeline behavior.
- **AI adds depth for humans** — recruiters see both scores side-by-side. AI reasoning helps them make better decisions in the manual review zone.
- **Graceful degradation** — if the AI Service is unavailable, the pipeline functions normally. No applicant is blocked by an AI outage.
- **Cost control** — AI scoring is opt-in. Tenants only pay for AI API calls when they've explicitly enabled it.

---

## Scoring Pipeline

```
1. ApplicationSubmittedEvent received
2. screening_results row created (status = Pending)
3. Processor picks up the job:
   a. Status → InProgress, started_at = now
   b. Fetch tenant's screening configuration from Admin (CompanySettings)
   c. Fetch the application's resume record via resume_id (from profiles.resumes)
      → ai_parsed_content is used if available (AI-parsed structured data)
      → Falls back to basic parsed_text and extracted_skills if AI parsing failed or is disabled
   d. Fetch applicant's profile data from Profiles module
   e. Fetch job_evaluation_criteria from Recruitment
   f. DETERMINISTIC SCORING (local, always runs):
      → Score each criterion using ExactMatch/RangeMatch/keyword-based SemanticSimilarity
      → Build criteria_score_breakdown
      → Compute overall_score using criteria weights
   g. AI ANALYSIS SCORING (conditional — only if system gate ON + tenant ai_scoring_enabled):
      → Call POST /api/v1/ai/screening/evaluate with resume data + criteria
      → Build ai_criteria_score_breakdown
      → Compute ai_overall_score
      → If disabled or AI Service unavailable, these fields stay NULL
   h. If AtApplication questions exist:
      → Fetch screening_question_responses for this application
      → MultipleChoice/YesNo: score deterministically (always)
      → FreeText: call AI Service POST /api/v1/ai/screening/score-answers
         (always — independent of AI scoring flag; no deterministic path for free text)
      → Build question_score_breakdown
      → Update individual screening_question_responses rows with scores
   i. Derive match_strength from deterministic overall_score ranges
   j. Capture auto_advance_threshold and auto_reject_threshold on the row
   k. If candidate_transparency enabled:
      → Call AI Service POST /api/v1/ai/screening/feedback → candidate_feedback
4. Status → Completed, completed_at = now
5. Determine routing (based on DETERMINISTIC overall_score only):
   a. Score >= auto_advance_threshold:
      → outcome = AutoAdvanced
      → If job has AfterScreening questions: application status → Assessment
      → If no AfterScreening questions: application status → Shortlisted
   b. Score <= auto_reject_threshold:
      → outcome = AutoRejected
      → Application status → Rejected
   c. Score between thresholds → apply tenant's manual_review_policy:
      → QueueForReview / NotifyAndHold: outcome = ManualReview (application stays at Screening)
      → AutoAdvanceAll: outcome = AutoAdvanced (same as 5a)
      → AutoRejectAll: outcome = AutoRejected (same as 5b)
6. Publish CvScreeningCompletedEvent (domain event → Matching) in all cases
```

---

## Assessment Flow

The Assessment stage is **optional** — it only exists when a job has `AfterScreening` questions. Jobs with only `AtApplication` questions (or no questions at all) skip straight from screening to Shortlisted.

```
1. Candidate reaches Assessment stage (auto-advanced or manually advanced from screening)
2. AfterScreening questions are presented to the candidate
3. Candidate submits answers → screening_question_responses rows created
4. Assessment answers scored:
   → MultipleChoice/YesNo: deterministic
   → FreeText: AI Service (always — no deterministic path)
   → question_score_breakdown updated on screening_results
   → assessment_score computed from question weights
5. Final pipeline score = weighted combination:
   screening overall_score × screening_weight + assessment_score × assessment_weight
   (weights from matching_settings: screening_weight + assessment_weight, sum to 100)
6. Apply completion_policy from assessment_settings:
   a. AutoAdvance → advance to Shortlisted
   b. QueueForReview → recruiter reviews answers before advancing
7. Publish AssessmentCompletedEvent (domain event → Matching)
```

---

## Manual Review Flow

```
1. Screening completes with outcome = ManualReview
2. Application appears in recruiter's "Needs Review" queue
   (filtered by: screening_results.outcome = 'ManualReview')
3. Recruiter reviews:
   - Deterministic scores (criteria_score_breakdown, overall_score)
   - AI analysis scores (ai_criteria_score_breakdown, ai_overall_score) — if available
   - Question answers and scores — if applicable
4. Recruiter decides:
   a. Advance → outcome updated to ManuallyAdvanced
      → reviewed_by, reviewed_at, review_notes set
      → If job has AfterScreening questions: application status → Assessment
      → If no AfterScreening questions: application status → Shortlisted
   b. Reject → outcome updated to ManuallyRejected
      → reviewed_by, reviewed_at, review_notes set
      → Application status → Rejected
```

---

## Re-Scoring

When a recruiter modifies a job's evaluation criteria after screening has already run for some applications:

1. Affected `screening_results` rows are marked for re-evaluation (via a flag or status change).
2. The scoring pipeline re-runs for each affected application.
3. `criteria_score_breakdown` is regenerated, `overall_score` recalculated.
4. If AI scoring is enabled, `ai_criteria_score_breakdown` and `ai_overall_score` are also regenerated.
5. Routing may change — an application previously in `ManualReview` might now be `AutoAdvanced`.
6. Original thresholds are preserved; new thresholds are captured on the updated result.

---

## Candidate Transparency

Configurable per tenant via `screening_settings`:

- **`candidate_transparency_enabled`**: Whether to generate candidate-facing feedback at all. Default: `false`.
- **`candidate_transparency_level`**: How much detail to show. Enum: `None`, `Summary`, `Detailed`. Default: `Summary`.
  - `None`: No feedback generated (same as disabled).
  - `Summary`: Overall fit assessment + high-level strengths and gaps.
  - `Detailed`: Per-criterion breakdown with specific guidance for improvement.

Candidate feedback is generated during the scoring pipeline (step 3k) by calling the AI Service's feedback endpoint. The result is stored in `candidate_feedback` on the screening result row and exposed via a candidate-facing endpoint.

---

## Error Handling & Retries

If screening fails (resume read error, AI Service timeout, unexpected data format), the row is marked `status = Failed` with a `failure_reason`. This is distinct from a low score or auto-rejection — processing failures are technical errors, not evaluations.

Failed screenings can be retried. The retry mechanism picks up rows where `status = Failed` and reprocesses them. The `failure_reason` helps with debugging and deciding whether to retry (transient network error → retry, unsupported file format → don't retry, notify the applicant).

---

## Design Decisions

**Criteria-based scoring replaces fixed sub-scores.** The original design had `skill_match_score`, `experience_match_score`, and `resume_quality_score` as separate columns. These are replaced by `criteria_score_breakdown` JSONB — a flexible per-criterion breakdown that supports any number and type of criteria. `overall_score` handles ranking; the criteria breakdown provides all the detail. This avoids maintaining parallel rollup columns that become stale when criteria change.

**Assessment score is separate from screening score.** The `assessment_score` from `AfterScreening` questions is stored independently from the resume-based `overall_score`. The final pipeline score is a weighted combination (configurable via `matching_settings`). This avoids re-scoring the entire screening result when assessment answers come in — only the assessment portion updates.

**Dual scoring engine with feature flags.** Deterministic scoring is always available and drives all automation. AI analysis scoring is opt-in (two-layer flag: system gate + tenant opt-in) and advisory only. This ensures the pipeline always works, AI costs are controlled, and recruiter decisions can be enhanced without changing pipeline behavior.

**FreeText question scoring always uses AI.** Unlike `MultipleChoice`/`YesNo` questions which can be scored deterministically (correct answer comparison), `FreeText` responses require understanding to score meaningfully. This is independent of the `ai_scoring_enabled` flag — FreeText scoring always calls the AI Service.

**Three-tier routing model.** Top tier auto-advances (clear strong matches shouldn't wait for human review), bottom tier auto-rejects (obvious mismatches shouldn't waste recruiter time), middle tier gets human judgment. The thresholds are tenant-configurable because every company has different standards and applicant volumes.

**Thresholds captured on the result row.** Tenants can change their thresholds at any time. Storing the thresholds that were active at evaluation time means you can always reconstruct why a particular routing decision was made.

**JSONB for criteria and question breakdowns.** These breakdowns are always read and written as complete objects — never queried field-by-field across screening results. JSONB keeps the schema flat and avoids join tables that would add complexity for no queryability benefit.

**`screening_question_responses` in the Screening schema with cross-schema reference.** Answers belong to the screening/evaluation domain, not to Recruitment. Integrity with `recruitment.job_screening_questions` is enforced at the application layer via SharedKernel interfaces — no DB-level FK constraint. Storing answers in Screening keeps the evaluation data together and avoids splitting the scoring data across modules.

**Resume parsing lives in Profiles, not Screening.** Resumes are parsed once on upload by the Profiles module. AI-powered structured extraction (when enabled) produces `ai_parsed_content` with skills, experience timeline, education, and certifications. Screening reads this pre-parsed data — no downloading, no re-parsing, no duplicate storage. Basic fallback parsing is always available.

**Shared primary key with `recruitment.applications`.** One screening result per application, enforced at the database level. Same pattern used throughout the project.

**`outcome` separate from screening `status` and application `status`.** Three things are tracked independently: the screening process (Pending → InProgress → Completed), the routing decision (AutoAdvanced / ManualReview / etc.), and the application pipeline stage (Screening → Assessment / Shortlisted / Rejected). Conflating them would make queries confusing and state transitions error-prone.

**`Failed` status for processing errors, not for low-scoring candidates.** A processing failure means "we couldn't evaluate this person" — it should be retried. A low score with `AutoRejected` outcome means "we evaluated them and they didn't meet the bar." Conflating these would make retry logic dangerous.

**Cross-schema reference to `recruitment.applications` (no DB-level FK constraint).** Screening is tightly coupled to Recruitment by design — it can't exist without an application. The `ApplicationSubmittedEvent` domain event guarantees the source application exists before screening begins.
