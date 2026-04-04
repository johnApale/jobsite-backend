# Recruitment Module — Database Design

Core of the hiring pipeline. Manages job postings and application intake. Every other module downstream — Screening, Matching, HR Workflows — hangs off the application record created here. The application is the spine of the entire hiring process.

Publishes `ApplicationSubmittedEvent` when a new application comes in, which kicks off the automated pipeline (Screening → Assessment → Matching → HR Workflows).

---

## Tables

### client_companies

Companies that the tenant recruits on behalf of. Only relevant for agency tenants — regular companies hiring for themselves don't use this table. Each client company represents an external employer whose jobs the agency manages.

| Column        | Type          | Constraints             | Description                                                                                                                  |
| ------------- | ------------- | ----------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| id            | uuid          | PK                      |                                                                                                                              |
| name          | varchar(200)  | NOT NULL                | Client company name (e.g., "Google", "Meta")                                                                                 |
| display_name  | varchar(200)  | nullable                | Public-facing name shown on job listings. NULL = use `name`. Set to something like "Top Tech Company" for anonymous postings |
| is_anonymous  | boolean       | NOT NULL, DEFAULT false | Whether to hide the real company name on job listings. When true, `display_name` is shown instead                            |
| industry      | varchar(100)  | nullable                | Client's industry (e.g., "Technology", "Healthcare"). Useful for filtering and reporting                                     |
| website       | varchar(2048) | nullable                | Client company's website                                                                                                     |
| contact_name  | varchar(200)  | nullable                | Primary contact person at the client company                                                                                 |
| contact_email | varchar(254)  | nullable                | Contact email for the client                                                                                                 |
| contact_phone | varchar(20)   | nullable                | Contact phone for the client                                                                                                 |
| notes         | text          | nullable                | Internal notes about the client relationship (not shown to applicants)                                                       |
| status        | varchar(20)   | NOT NULL                | Enum: `Active`, `Inactive`. Inactive clients can't have new jobs posted                                                      |
| created_at    | timestamp     | NOT NULL                |                                                                                                                              |
| updated_at    | timestamp     | NOT NULL                | Auto-set on modification                                                                                                     |

**Indexes:**

| Name                       | Columns | Type       | Purpose               |
| -------------------------- | ------- | ---------- | --------------------- |
| ix_client_companies_name   | name    | Non-unique | Search by client name |
| ix_client_companies_status | status  | Non-unique | Filter active clients |

---

### job_postings

A job that applicants can apply to. Created by Recruiters or HiringManagers, goes through a draft → publish → close lifecycle. Contains the human-readable description. Structured evaluation criteria and screening questions are stored in separate tables (`job_evaluation_criteria` and `job_screening_questions`).

For agency tenants, `client_company_id` links the job to the external company being hired for. For regular company tenants hiring for themselves, this is NULL.

| Column            | Type          | Constraints                        | Description                                                                                  |
| ----------------- | ------------- | ---------------------------------- | -------------------------------------------------------------------------------------------- |
| id                | uuid          | PK                                 |                                                                                              |
| client_company_id | uuid          | nullable, FK → client_companies.id | The client company this job is for. NULL if the tenant is hiring for themselves (non-agency) |
| title             | varchar(200)  | NOT NULL                           | Job title (e.g., "Senior .NET Developer")                                                    |
| description       | text          | NOT NULL                           | Full job description. Free-form rich text for the applicant-facing listing                   |
| requirements      | text          | nullable                           | Free-form text describing job requirements. Used by AI to suggest evaluation criteria        |
| location_type     | varchar(20)   | NOT NULL                           | Enum: `OnSite`, `Remote`, `Hybrid`                                                           |
| city              | varchar(100)  | nullable                           | Required for `OnSite` and `Hybrid`. NULL for fully remote                                    |
| country           | varchar(100)  | nullable                           | Required for `OnSite` and `Hybrid`. NULL for fully remote                                    |
| employment_type   | varchar(20)   | NOT NULL                           | Enum: `FullTime`, `PartTime`, `Contract`, `Temporary`, `Internship`                          |
| salary_min        | decimal(12,2) | nullable                           | Minimum salary. Nullable because not all postings disclose salary                            |
| salary_max        | decimal(12,2) | nullable                           | Maximum salary                                                                               |
| salary_currency   | varchar(3)    | nullable                           | ISO 4217 currency code (e.g., `USD`, `EUR`). Required if salary is provided                  |

| department | varchar(100) | nullable | Organizational department (e.g., "Engineering", "Marketing") |
| status | varchar(20) | NOT NULL | Enum: `Draft`, `Published`, `Closed` |
| posted_by | uuid | NOT NULL, FK → auth.users.id | The Recruiter or HiringManager who created this posting |
| published_at | timestamp | nullable | Set when status moves to `Published`. Used for "newest jobs" sorting |
| closes_at | timestamp | nullable | Optional auto-close date. Job stops accepting applications after this |
| closed_at | timestamp | nullable | Set when status moves to `Closed` (manually or via auto-close) |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name                           | Columns           | Type       | Purpose                                                         |
| ------------------------------ | ----------------- | ---------- | --------------------------------------------------------------- |
| ix_job_postings_client_company | client_company_id | Non-unique | "All jobs for this client company" — agency dashboard filtering |
| ix_job_postings_status         | status            | Non-unique | Filter published jobs for applicant-facing listing              |
| ix_job_postings_posted_by      | posted_by         | Non-unique | "My job postings" queries for recruiters/hiring managers        |
| ix_job_postings_published_at   | published_at      | Non-unique | Sort by newest. Nullable — only set on published jobs           |
| ix_job_postings_location       | city, country     | Non-unique | Location-based job search                                       |

---

### applications

An applicant's submission to a specific job posting. This is the central record that the entire hiring pipeline operates on. Every downstream module (Screening, Matching, HR Workflows) references the application — not the applicant or job directly.

One application per applicant per job, enforced by a unique constraint. The application references the specific resume version from `profiles.resumes` that was current at submission time, so re-uploads to the profile don't affect in-flight applications.

| Column            | Type          | Constraints                        | Description                                                                                                                                                                                       |
| ----------------- | ------------- | ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| id                | uuid          | PK                                 |                                                                                                                                                                                                   |
| job_posting_id    | uuid          | NOT NULL, FK → job_postings.id     | The job this application is for                                                                                                                                                                   |
| applicant_id      | uuid          | NOT NULL, FK → auth.users.id       | The applicant who submitted. Must have role = `Applicant` (enforced in app code)                                                                                                                  |
| status            | varchar(20)   | NOT NULL                           | Enum: `Submitted`, `Screening`, `Assessment`, `Shortlisted`, `FinalInterview`, `Offered`, `Hired`, `Rejected`, `Withdrawn`                                                                        |
| resume_id         | uuid          | NOT NULL, FK → profiles.resumes.id | The specific resume version submitted with this application. Points to the resume that was `is_latest` at submission time. Immutable — doesn't change if the applicant uploads a new resume later |
| cover_letter_url  | varchar(2048) | nullable                           | Optional cover letter submitted with this specific application                                                                                                                                    |
| rejection_reason  | varchar(500)  | nullable                           | Set when status moves to `Rejected`. Brief explanation (e.g., "Did not meet minimum skill requirements")                                                                                          |
| rejected_at_stage | varchar(20)   | nullable                           | Enum: `Screening`, `Assessment`, `Shortlisted`, `FinalInterview`, `Offered`. Which pipeline stage rejected the candidate                                                                          |
| withdrawn_at      | timestamp     | nullable                           | Set when applicant withdraws. Status moves to `Withdrawn`                                                                                                                                         |
| submitted_at      | timestamp     | NOT NULL                           | When the application was submitted. Distinct from `created_at` for clarity                                                                                                                        |
| created_at        | timestamp     | NOT NULL                           |                                                                                                                                                                                                   |
| updated_at        | timestamp     | NOT NULL                           | Auto-set on modification — tracks status transitions                                                                                                                                              |

**Constraints:**

| Name                          | Columns                      | Type   | Purpose                                                            |
| ----------------------------- | ---------------------------- | ------ | ------------------------------------------------------------------ |
| uq_applications_applicant_job | applicant_id, job_posting_id | Unique | One application per person per job. Prevents duplicate submissions |

**Indexes:**

| Name                           | Columns        | Type       | Purpose                                                          |
| ------------------------------ | -------------- | ---------- | ---------------------------------------------------------------- |
| ix_applications_job_posting_id | job_posting_id | Non-unique | "All applications for this job" — the most common query          |
| ix_applications_applicant_id   | applicant_id   | Non-unique | "All jobs this person applied to"                                |
| ix_applications_status         | status         | Non-unique | Filter by pipeline stage (e.g., "all applications in Screening") |
| ix_applications_submitted_at   | submitted_at   | Non-unique | Sort by newest, time-range queries for dashboards                |

---

### job_evaluation_criteria

Structured evaluation criteria for a job posting. Replaces the legacy `required_skills` and `nice_to_have_skills` JSONB columns with a normalized, per-criterion table that supports multiple categories, evaluation methods, and configurable weights. This is what the Screening module evaluates candidates against.

Recruiters configure these manually or use AI-assisted suggestions (via the AI Service's `/api/v1/ai/criteria/suggest` endpoint). Tenants can also set default criteria templates in `screening_settings.default_evaluation_criteria` that are copied to new jobs as a starting point.

| Column            | Type         | Constraints                    | Description                                                                                                                                                                    |
| ----------------- | ------------ | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| id                | uuid         | PK                             |                                                                                                                                                                                |
| job_posting_id    | uuid         | NOT NULL, FK → job_postings.id | The job posting these criteria belong to                                                                                                                                       |
| name              | varchar(200) | NOT NULL                       | Human-readable criterion name (e.g., "C# Proficiency", "5+ Years Backend Experience")                                                                                          |
| category          | varchar(20)  | NOT NULL                       | Enum: `Skill`, `Experience`, `Certification`, `Education`, `Location`, `Custom`. Determines the `configuration` JSONB shape                                                    |
| evaluation_method | varchar(30)  | NOT NULL                       | Enum: `ExactMatch`, `RangeMatch`, `SemanticSimilarity`. How the Screening module scores this criterion. See Evaluation Methods below                                           |
| is_required       | boolean      | NOT NULL, DEFAULT true         | Whether this is a hard requirement (pass/fail) or a nice-to-have (score booster). Required criteria that score `Missing` may trigger auto-rejection depending on tenant policy |
| weight            | decimal(5,2) | NOT NULL                       | Contribution to the overall screening score (0.00–100.00). All weights for a job should sum to 100. Enforced in application code                                               |
| configuration     | jsonb        | NOT NULL                       | Category-specific configuration. Shape depends on `category`. See Criteria Configuration Formats below                                                                         |
| display_order     | integer      | NOT NULL                       | Ordering for display in the recruiter UI                                                                                                                                       |
| created_at        | timestamp    | NOT NULL                       |                                                                                                                                                                                |
| updated_at        | timestamp    | NOT NULL                       | Auto-set on modification                                                                                                                                                       |

**Indexes:**

| Name                       | Columns        | Type       | Purpose                                                  |
| -------------------------- | -------------- | ---------- | -------------------------------------------------------- |
| ix_criteria_job_posting_id | job_posting_id | Non-unique | "All criteria for this job" — the primary access pattern |
| ix_criteria_category       | category       | Non-unique | Filter criteria by category for reporting and defaults   |

---

### job_screening_questions

Screening questions attached to a job posting. These are recruiter-defined questions that candidates answer either at application time (`AtApplication`) or after passing the resume screening stage (`AfterScreening`, in the Assessment stage). Questions can be created manually or suggested by the AI Service (feature-flagged via `ai_assessment_questions_enabled`).

| Column          | Type         | Constraints                    | Description                                                                                                                                                                                                                                                                  |
| --------------- | ------------ | ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| id              | uuid         | PK                             |                                                                                                                                                                                                                                                                              |
| job_posting_id  | uuid         | NOT NULL, FK → job_postings.id | The job posting these questions belong to                                                                                                                                                                                                                                    |
| question_text   | text         | NOT NULL                       | The question presented to the candidate                                                                                                                                                                                                                                      |
| question_type   | varchar(20)  | NOT NULL                       | Enum: `FreeText`, `MultipleChoice`, `YesNo`. Determines the answer format and scoring approach                                                                                                                                                                               |
| timing          | varchar(20)  | NOT NULL                       | Enum: `AtApplication`, `AfterScreening`. When the candidate sees this question. `AtApplication` = on the application form. `AfterScreening` = in the Assessment stage after passing resume screening                                                                         |
| is_required     | boolean      | NOT NULL, DEFAULT true         | Whether the candidate must answer this question                                                                                                                                                                                                                              |
| weight          | decimal(5,2) | NOT NULL                       | Contribution to the question score component. All question weights for a job should sum to 100                                                                                                                                                                               |
| expected_answer | jsonb        | nullable                       | Rubric or expected response for scoring. For `YesNo`: `{ "correct": true }`. For `MultipleChoice`: `{ "correct_options": [0, 2], "partial_credit": true }`. For `FreeText`: `{ "key_topics": ["scalability", "caching"], "scoring_guidance": "Look for specific examples" }` |
| options         | jsonb        | nullable                       | For `MultipleChoice` only. Array of option strings: `["Option A", "Option B", "Option C"]`. NULL for other types                                                                                                                                                             |
| display_order   | integer      | NOT NULL                       | Ordering within the question set                                                                                                                                                                                                                                             |
| created_at      | timestamp    | NOT NULL                       |                                                                                                                                                                                                                                                                              |
| updated_at      | timestamp    | NOT NULL                       | Auto-set on modification                                                                                                                                                                                                                                                     |

**Indexes:**

| Name                        | Columns        | Type       | Purpose                                                                  |
| --------------------------- | -------------- | ---------- | ------------------------------------------------------------------------ |
| ix_questions_job_posting_id | job_posting_id | Non-unique | "All questions for this job"                                             |
| ix_questions_timing         | timing         | Non-unique | Filter by when questions are presented (AtApplication vs AfterScreening) |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS recruitment;
```

All Recruitment module tables live under the `recruitment` schema. Downstream modules (Screening, Matching, HR Workflows) access application data through shared interfaces — not by querying these tables directly.

---

## Relationships

```
client_companies       ||--o{ job_postings           : "has many (agency posts jobs for client)"
job_postings           ||--o{ applications            : "has many"
job_postings           ||--o{ job_evaluation_criteria : "has many"
job_postings           ||--o{ job_screening_questions : "has many"
auth.users             ||--o{ applications            : "applicant submits many (one per job)"
auth.users             ||--o{ job_postings            : "staff member posts many"
profiles.resumes       ||--o{ applications            : "resume version used in application"
```

Cross-schema FKs to `auth.users` for both `posted_by` and `applicant_id`, and to `profiles.resumes` for `resume_id`. User identity and resume versioning are foundational — referential integrity matters more than strict schema isolation here.

The `client_companies → job_postings` relationship is optional. For non-agency tenants, `client_company_id` is NULL and the `client_companies` table stays empty.

`job_evaluation_criteria` and `job_screening_questions` are always loaded with the job posting for screening. Questions have a cross-schema consumer: `screening.screening_question_responses` references `job_screening_questions.id`.

---

## Criteria Configuration Formats

Each `job_evaluation_criteria` row has a `configuration` JSONB column whose shape depends on the `category`. These configurations tell the Screening module's scoring engine exactly what to evaluate and how.

### Skill

```json
{
  "skill_name": "C#",
  "min_level": "Intermediate",
  "min_years": 5
}
```

**`skill_name`** (required): The skill name to match against the applicant's profile and resume. Application-level normalization handles variations (e.g., "C#" vs "CSharp").

**`min_level`** (optional): Minimum required proficiency. Enum: `Beginner`, `Intermediate`, `Advanced`, `Expert`. If omitted, any level satisfies the requirement.

**`min_years`** (optional): Minimum years of experience with this skill. If omitted, any experience satisfies the requirement.

### Experience

```json
{
  "min_years": 3,
  "max_years": null,
  "domain": "Backend Development"
}
```

**`min_years`** (required): Minimum total years of professional experience.

**`max_years`** (optional): Maximum years. Useful for entry-level roles where over-qualification is a concern. NULL = no cap.

**`domain`** (optional): Free-text domain context for semantic matching (e.g., "Backend Development", "Healthcare IT"). When present with `SemanticSimilarity` evaluation method, the engine looks for relevant experience in this domain.

### Certification

```json
{
  "certification_name": "AWS Solutions Architect",
  "issuer": "Amazon",
  "required_level": null
}
```

**`certification_name`** (required): The certification to look for in the applicant's profile and resume.

**`issuer`** (optional): Expected issuing organization. Used for disambiguation when multiple providers offer similarly named certifications.

**`required_level`** (optional): Specific level if the certification has tiers (e.g., "Associate", "Professional").

### Education

```json
{
  "degree_level": "Bachelors",
  "field_of_study": "Computer Science",
  "acceptable_alternatives": ["Software Engineering", "Information Technology"]
}
```

**`degree_level`** (required): Minimum degree level. Enum: `HighSchool`, `Associates`, `Bachelors`, `Masters`, `Doctorate`.

**`field_of_study`** (required): Primary field of study to match.

**`acceptable_alternatives`** (optional): Additional fields that satisfy the requirement. Evaluated as OR — any match counts.

### Location

```json
{
  "allowed_locations": ["New York", "Remote"],
  "location_type": "Hybrid"
}
```

**`allowed_locations`** (required): List of acceptable locations. Matched against the application's or profile's location data.

**`location_type`** (required): Expected work arrangement. Enum values match `job_postings.location_type`: `OnSite`, `Remote`, `Hybrid`.

### Custom

```json
{
  "description": "Must have experience with large-scale distributed systems",
  "scoring_guidance": "Look for mentions of microservices, distributed databases, event-driven architecture"
}
```

**`description`** (required): Free-form description of the requirement. Scored by `SemanticSimilarity` against the applicant's resume and profile.

**`scoring_guidance`** (optional): Hints for the scoring engine on what to look for. Used by both the deterministic keyword fallback and AI analysis.

---

## Evaluation Methods

The `evaluation_method` on each criterion determines how the Screening module scores it:

- **ExactMatch** — Binary match. The applicant either has the requirement or doesn't. Best for: certifications, education degrees, Yes/No criteria. Scores: `MeetsRequirement` or `Missing`.

- **RangeMatch** — Compares a numeric value against a range. Best for: years of experience, skill levels. Supports partial credit — "has 3 years when 5 are required" scores proportionally. Scores: `MeetsRequirement`, `PartialMatch`, or `Missing`.

- **SemanticSimilarity** — Compares free-text content against the criterion description. In the **deterministic scoring engine**: uses keyword matching and text overlap as a basic fallback. In the **AI analysis scoring engine** (feature-flagged): uses LLM-based semantic analysis for deeper understanding. Best for: Custom criteria, domain experience, soft skills.

---

## Criteria Templates (Future Consideration)

Reusable criteria templates that recruiters can create, name, and apply to new jobs. The current design supports this workflow:

1. Tenant-level defaults in `screening_settings.default_evaluation_criteria` serve as the base — copied to every new job posting.
2. Recruiters customize per-job by adding, removing, or modifying criteria rows.
3. AI Service can suggest criteria from the job description (always available).

A dedicated `criteria_templates` table (with name, description, and a template_criteria JSONB) can be added later to let recruiters save custom templates beyond the tenant default. The existing `job_evaluation_criteria` table structure supports this without changes — templates would simply be copied into criteria rows on a new job.

---

## Job Posting Status Lifecycle

```
Draft → Published → Closed
```

- **Draft**: Job is being written. Not visible to applicants. Cannot receive applications.
- **Published**: Live on the tenant's job board. Accepting applications. `published_at` is set.
- **Closed**: No longer accepting applications. `closed_at` is set. Can happen manually (recruiter closes it) or automatically (via `closes_at` deadline). Existing applications continue through the pipeline — closing a job doesn't reject in-flight candidates.

No "Archived" state — a closed job stays closed. If they want to re-hire for the same role, they create a new posting (possibly cloning the old one in the UI).

---

## Application Status Lifecycle

```
Submitted → Screening → [Assessment] → Shortlisted → FinalInterview → Offered → Hired
                ↓                ↓             ↓               ↓            ↓
             Rejected      Rejected      Rejected        Rejected     Rejected

Any stage → Withdrawn (applicant pulls out)
```

- **Submitted**: Application received. `ApplicationSubmittedEvent` published → Screening module picks it up.
- **Screening**: CV screening in progress. Screening module evaluates the resume against job evaluation criteria.
- **Assessment**: Passed screening. Candidate is answering recruiter-defined `AfterScreening` questions. This stage is **optional** — only entered when the job has `AfterScreening` questions. Jobs with only `AtApplication` questions (or no questions) skip straight to `Shortlisted`.
- **Shortlisted**: Passed screening (and assessment, if applicable). Matching module has ranked the candidate and placed them on the shortlist.
- **FinalInterview**: HR has scheduled an in-person interview.
- **Offered**: Job offer extended. Waiting for candidate response.
- **Hired**: Offer accepted. Terminal success state.
- **Rejected**: Eliminated at any stage. `rejected_at_stage` records where. `rejection_reason` provides context. Terminal failure state.
- **Withdrawn**: Applicant pulled out voluntarily. `withdrawn_at` is set. Terminal state.

Status transitions are driven by domain events from downstream modules. Recruitment doesn't decide when to move to `Screening` or `Shortlisted` — it reacts to events from the modules that own those pipeline stages.

---

## Event Flow

```
1. Applicant submits application
2. Application created (status = Submitted)
3. ApplicationSubmittedEvent published (domain event, in-process event bus)
4. Screening module consumes event → begins CV evaluation against job_evaluation_criteria
   → Updates application status to Screening
   → If job has AtApplication questions, candidate answers are stored and scored
5. Screening completes:
   → Pass + job has AfterScreening questions:
          Application status → Assessment
          Candidate is presented with AfterScreening questions
   → Pass + no AfterScreening questions:
          Application status → Shortlisted
          CvScreeningCompletedEvent published (domain event → Matching)
   → Fail: Application status → Rejected (rejected_at_stage = Screening)
6. Assessment completes (if applicable):
   → Answers scored, assessment_score computed
   → completion_policy applied (AutoAdvance or QueueForReview)
   → AssessmentCompletedEvent published (domain event → Matching)
   → Application status → Shortlisted (or remains at Assessment if QueueForReview)
7. Matching scores and ranks:
   → Shortlisted: Application status → Shortlisted (if not already)
   → Not shortlisted: Application status → Rejected (rejected_at_stage = Shortlisted)
8. HR schedules final interview → Application status → FinalInterview
9. HR extends offer → Application status → Offered
10. Candidate accepts → Application status → Hired
```

---

## Resume Versioning

The application stores a `resume_id` FK pointing to the specific `profiles.resumes` record that was `is_latest` at submission time. This is intentional — if the applicant uploads a new resume after applying, the in-flight application still references the original version they submitted with.

The Screening module follows the `resume_id` to get the `file_url`, `parsed_text`, and `extracted_skills` from the resume record. Because the resume is parsed once on upload (by the Profiles module's background parser), Screening doesn't need to re-download or re-parse the file — it reads the pre-parsed data directly. This means ten applications with the same resume all reference the same parsed data with zero duplication.

---

## Design Decisions

**Normalized `job_evaluation_criteria` table instead of JSONB skill columns.** The original `required_skills` and `nice_to_have_skills` JSONB columns were limited to skills only and made it hard to weight, categorize, or extend evaluation criteria independently. The normalized table supports multiple criteria categories (Skill, Experience, Certification, Education, Location, Custom), per-criterion weights, configurable evaluation methods, and CRUD operations on individual criteria without rewriting the entire JSONB blob. The trade-off is more rows and joins — but criteria are always loaded as a set with the job posting, and the count per job is small (typically 5–15 criteria).

**`job_screening_questions` with configurable timing.** Questions can be presented at two points: `AtApplication` (on the submission form, answers stored immediately in `screening.screening_question_responses`) or `AfterScreening` (in the Assessment stage, after resume screening passes). This two-timing design lets recruiters front-load simple screening questions while reserving deeper questions for candidates who pass the automated screen. Jobs without `AfterScreening` questions skip the Assessment stage entirely.

**Criteria configuration as JSONB with category-specific shapes.** Each criterion category has its own `configuration` JSONB structure (Skill has `skill_name`/`min_level`/`min_years`, Education has `degree_level`/`field_of_study`/`acceptable_alternatives`, etc.). This keeps the table schema flat while allowing rich per-category configuration. The alternative — separate tables per category — would create six join tables for a feature that's always loaded as one set.

**`client_companies` table with optional FK on job postings.** Supports the agency use case where tenants recruit on behalf of external companies. The FK is nullable — non-agency tenants hiring for themselves leave it NULL, and the `client_companies` table stays empty. The `is_anonymous` flag and `display_name` column handle the common scenario where agencies want to post jobs without revealing the client ("Top Tech Company" instead of "Google"). Contact fields on the client company keep the agency's client relationship info in one place.

**`resume_id` FK instead of a `resume_url` snapshot.** The application points to a specific `profiles.resumes` record — the version that was current at submission time. This is better than copying the URL because: the resume's parsed text and extracted skills are already stored on the resume record (parsed once on upload), so Screening reads pre-parsed data without re-downloading or re-parsing; multiple applications with the same resume share one parsed copy instead of duplicating; and the full resume metadata (file type, size, parse status) is available without denormalizing it onto every application.

**`rejected_at_stage` separate from `status`.** When an application is `Rejected`, you need to know where it was rejected. Was it screening? Assessment? Final interview? This field records the stage, which is critical for funnel analytics ("where are we losing candidates?") and for the applicant's feedback.

**`Withdrawn` as a status, not a soft delete.** Applicants should be able to pull out at any stage. This is a legitimate terminal state, not a deletion. The record stays for analytics and audit purposes.

**No `notes` or `tags` on applications.** Notes belong to the modules that evaluate the application (Screening has scores, HR Workflows has interview notes). The application itself is a pipeline record — it tracks status, not commentary. Adding generic notes here would blur module boundaries.

**`closes_at` for auto-close, `closed_at` for actual close.** `closes_at` is a future deadline set when the job is published. A background job checks this and closes expired postings. `closed_at` records when the job actually closed, whether via auto-close or manual action. Both are useful for different purposes.

**Cross-schema FKs to `auth.users`.** Both `posted_by` (staff) and `applicant_id` (applicant) reference `auth.users`. Same justification as Profiles — user identity is foundational. The FK on `applicant_id` ensures you can't have an application for a deleted user, and `posted_by` ensures every job has a traceable creator.

**No `application_documents` join table.** The application references a resume via `resume_id` and has an optional `cover_letter_url` inline. These are the only two documents relevant to a specific application (as opposed to the profile's general document collection). If applications ever need arbitrary additional documents, a JSONB column like the profile's `documents` can be added — but the current design is simpler and sufficient.

**Per-module schema.** `recruitment.*` keeps these tables clearly owned by this module. Screening, Matching, and HR Workflows reference applications through shared interfaces — they don't query `recruitment.applications` directly.
