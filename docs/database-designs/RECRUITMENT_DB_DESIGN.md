# Recruitment Module — Database Design

Core of the hiring pipeline. Manages job postings and application intake. Every other module downstream — Screening, Matching, HR Workflows — hangs off the application record created here. The application is the spine of the entire hiring process.

Publishes `ApplicationSubmittedEvent` when a new application comes in, which kicks off the automated pipeline (Screening → AI Interview → Matching → HR Workflows).

---

## Tables

### client_companies

Companies that the tenant recruits on behalf of. Only relevant for agency tenants — regular companies hiring for themselves don't use this table. Each client company represents an external employer whose jobs the agency manages.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| name | varchar(200) | NOT NULL | Client company name (e.g., "Google", "Meta") |
| display_name | varchar(200) | nullable | Public-facing name shown on job listings. NULL = use `name`. Set to something like "Top Tech Company" for anonymous postings |
| is_anonymous | boolean | NOT NULL, DEFAULT false | Whether to hide the real company name on job listings. When true, `display_name` is shown instead |
| industry | varchar(100) | nullable | Client's industry (e.g., "Technology", "Healthcare"). Useful for filtering and reporting |
| website | varchar(2048) | nullable | Client company's website |
| contact_name | varchar(200) | nullable | Primary contact person at the client company |
| contact_email | varchar(254) | nullable | Contact email for the client |
| contact_phone | varchar(20) | nullable | Contact phone for the client |
| notes | text | nullable | Internal notes about the client relationship (not shown to applicants) |
| status | varchar(20) | NOT NULL | Enum: `Active`, `Inactive`. Inactive clients can't have new jobs posted |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_client_companies_name | name | Non-unique | Search by client name |
| ix_client_companies_status | status | Non-unique | Filter active clients |

---

### job_postings

A job that applicants can apply to. Created by Recruiters or HiringManagers, goes through a draft → publish → close lifecycle. Contains both the human-readable description and the structured requirements that the Screening and Matching modules use for automated scoring.

For agency tenants, `client_company_id` links the job to the external company being hired for. For regular company tenants hiring for themselves, this is NULL.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| client_company_id | uuid | nullable, FK → client_companies.id | The client company this job is for. NULL if the tenant is hiring for themselves (non-agency) |
| title | varchar(200) | NOT NULL | Job title (e.g., "Senior .NET Developer") |
| description | text | NOT NULL | Full job description. Free-form rich text for the applicant-facing listing |
| location_type | varchar(20) | NOT NULL | Enum: `OnSite`, `Remote`, `Hybrid` |
| city | varchar(100) | nullable | Required for `OnSite` and `Hybrid`. NULL for fully remote |
| country | varchar(100) | nullable | Required for `OnSite` and `Hybrid`. NULL for fully remote |
| employment_type | varchar(20) | NOT NULL | Enum: `FullTime`, `PartTime`, `Contract`, `Temporary`, `Internship` |
| salary_min | decimal(12,2) | nullable | Minimum salary. Nullable because not all postings disclose salary |
| salary_max | decimal(12,2) | nullable | Maximum salary |
| salary_currency | varchar(3) | nullable | ISO 4217 currency code (e.g., `USD`, `EUR`). Required if salary is provided |
| required_skills | jsonb | NOT NULL, DEFAULT '[]' | Structured skill requirements for automated scoring. See Required Skills Format below |
| nice_to_have_skills | jsonb | NOT NULL, DEFAULT '[]' | Optional skills that boost a candidate's score but aren't required. Same format as required_skills |
| department | varchar(100) | nullable | Organizational department (e.g., "Engineering", "Marketing") |
| status | varchar(20) | NOT NULL | Enum: `Draft`, `Published`, `Closed` |
| posted_by | uuid | NOT NULL, FK → auth.users.id | The Recruiter or HiringManager who created this posting |
| published_at | timestamp | nullable | Set when status moves to `Published`. Used for "newest jobs" sorting |
| closes_at | timestamp | nullable | Optional auto-close date. Job stops accepting applications after this |
| closed_at | timestamp | nullable | Set when status moves to `Closed` (manually or via auto-close) |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_job_postings_client_company | client_company_id | Non-unique | "All jobs for this client company" — agency dashboard filtering |
| ix_job_postings_status | status | Non-unique | Filter published jobs for applicant-facing listing |
| ix_job_postings_posted_by | posted_by | Non-unique | "My job postings" queries for recruiters/hiring managers |
| ix_job_postings_published_at | published_at | Non-unique | Sort by newest. Nullable — only set on published jobs |
| ix_job_postings_location | city, country | Non-unique | Location-based job search |
| ix_job_postings_required_skills | required_skills | GIN | Skill-based job search (e.g., "jobs requiring C#") |

---

### applications

An applicant's submission to a specific job posting. This is the central record that the entire hiring pipeline operates on. Every downstream module (Screening, Matching, HR Workflows) references the application — not the applicant or job directly.

One application per applicant per job, enforced by a unique constraint. The application references the specific resume version from `profiles.resumes` that was current at submission time, so re-uploads to the profile don't affect in-flight applications.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| job_posting_id | uuid | NOT NULL, FK → job_postings.id | The job this application is for |
| applicant_id | uuid | NOT NULL, FK → auth.users.id | The applicant who submitted. Must have role = `Applicant` (enforced in app code) |
| status | varchar(20) | NOT NULL | Enum: `Submitted`, `Screening`, `AiInterview`, `Shortlisted`, `FinalInterview`, `Offered`, `Hired`, `Rejected`, `Withdrawn` |
| resume_id | uuid | NOT NULL, FK → profiles.resumes.id | The specific resume version submitted with this application. Points to the resume that was `is_latest` at submission time. Immutable — doesn't change if the applicant uploads a new resume later |
| cover_letter_url | varchar(2048) | nullable | Optional cover letter submitted with this specific application |
| rejection_reason | varchar(500) | nullable | Set when status moves to `Rejected`. Brief explanation (e.g., "Did not meet minimum skill requirements") |
| rejected_at_stage | varchar(20) | nullable | Which pipeline stage rejected the candidate. Same enum values as status — records where in the pipeline they were eliminated |
| withdrawn_at | timestamp | nullable | Set when applicant withdraws. Status moves to `Withdrawn` |
| submitted_at | timestamp | NOT NULL | When the application was submitted. Distinct from `created_at` for clarity |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification — tracks status transitions |

**Constraints:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| uq_applications_applicant_job | applicant_id, job_posting_id | Unique | One application per person per job. Prevents duplicate submissions |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_applications_job_posting_id | job_posting_id | Non-unique | "All applications for this job" — the most common query |
| ix_applications_applicant_id | applicant_id | Non-unique | "All jobs this person applied to" |
| ix_applications_status | status | Non-unique | Filter by pipeline stage (e.g., "all applications in Screening") |
| ix_applications_submitted_at | submitted_at | Non-unique | Sort by newest, time-range queries for dashboards |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS recruitment;
```

All Recruitment module tables live under the `recruitment` schema. Downstream modules (Screening, Matching, HR Workflows) access application data through shared interfaces — not by querying these tables directly.

---

## Relationships

```
client_companies ||--o{ job_postings : "has many (agency posts jobs for client)"
job_postings     ||--o{ applications : "has many"
auth.users       ||--o{ applications : "applicant submits many (one per job)"
auth.users       ||--o{ job_postings : "staff member posts many"
profiles.resumes ||--o{ applications : "resume version used in application"
```

Cross-schema FKs to `auth.users` for both `posted_by` and `applicant_id`, and to `profiles.resumes` for `resume_id`. User identity and resume versioning are foundational — referential integrity matters more than strict schema isolation here.

The `client_companies → job_postings` relationship is optional. For non-agency tenants, `client_company_id` is NULL and the `client_companies` table stays empty.

---

## Required Skills Format

Structured skill requirements that mirror the applicant's skills format in Profiles. This is how the Screening module connects job requirements to candidate profiles for automated scoring.

```json
[
  { "name": "C#", "min_level": "Intermediate", "min_years": 5 },
  { "name": "PostgreSQL", "min_level": "Beginner", "min_years": 2 },
  { "name": "Docker" }
]
```

**`name`** (required): The skill name. Should match the vocabulary applicants use in their profiles. Application-level normalization handles variations (e.g., "C#" vs "CSharp").

**`min_level`** (optional): Minimum required proficiency. Enum: `Beginner`, `Intermediate`, `Advanced`, `Expert`. If omitted, any level satisfies the requirement.

**`min_years`** (optional): Minimum years of experience with this skill. If omitted, any experience satisfies the requirement.

The `nice_to_have_skills` column uses the same format. The difference is in how Screening and Matching weight them — required skills are pass/fail filters, nice-to-have skills are score boosters.

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
Submitted → Screening → AiInterview → Shortlisted → FinalInterview → Offered → Hired
                ↓              ↓             ↓               ↓            ↓
             Rejected      Rejected      Rejected        Rejected     Rejected

Any stage → Withdrawn (applicant pulls out)
```

- **Submitted**: Application received. `ApplicationSubmittedEvent` published → Screening module picks it up.
- **Screening**: CV screening in progress. Screening module evaluates the resume against job requirements.
- **AiInterview**: Passed screening. AI Interview Service is generating and conducting the digital interview.
- **Shortlisted**: Passed AI interview. Matching module has ranked the candidate and placed them on the shortlist.
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
3. ApplicationSubmittedEvent published (domain event, MediatR)
4. Screening module consumes event → begins CV evaluation
   → Updates application status to Screening
5. Screening completes:
   → Pass: CandidateReadyForInterviewEvent (integration event → message broker → AI Interview Service)
          Application status → AiInterview
   → Fail: Application status → Rejected (rejected_at_stage = Screening)
6. AI Interview completes:
   → InterviewCompletedEvent (integration event → message broker → Matching)
7. Matching scores and ranks:
   → Shortlisted: Application status → Shortlisted
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

**Structured `required_skills` and `nice_to_have_skills` on job postings.** The description field is for humans. The skills JSONB is for machines. Screening needs structured data to do automated matching — free-text job descriptions would require NLP parsing on every evaluation. By having recruiters specify structured requirements (which the UI can make easy with dropdowns and sliders), the scoring pipeline gets clean input.

**`client_companies` table with optional FK on job postings.** Supports the agency use case where tenants recruit on behalf of external companies. The FK is nullable — non-agency tenants hiring for themselves leave it NULL, and the `client_companies` table stays empty. The `is_anonymous` flag and `display_name` column handle the common scenario where agencies want to post jobs without revealing the client ("Top Tech Company" instead of "Google"). Contact fields on the client company keep the agency's client relationship info in one place.

**`resume_id` FK instead of a `resume_url` snapshot.** The application points to a specific `profiles.resumes` record — the version that was current at submission time. This is better than copying the URL because: the resume's parsed text and extracted skills are already stored on the resume record (parsed once on upload), so Screening reads pre-parsed data without re-downloading or re-parsing; multiple applications with the same resume share one parsed copy instead of duplicating; and the full resume metadata (file type, size, parse status) is available without denormalizing it onto every application.

**`rejected_at_stage` separate from `status`.** When an application is `Rejected`, you need to know where it was rejected. Was it screening? AI interview? Final interview? This field records the stage, which is critical for funnel analytics ("where are we losing candidates?") and for the applicant's feedback.

**`Withdrawn` as a status, not a soft delete.** Applicants should be able to pull out at any stage. This is a legitimate terminal state, not a deletion. The record stays for analytics and audit purposes.

**No `notes` or `tags` on applications.** Notes belong to the modules that evaluate the application (Screening has scores, HR Workflows has interview notes). The application itself is a pipeline record — it tracks status, not commentary. Adding generic notes here would blur module boundaries.

**`closes_at` for auto-close, `closed_at` for actual close.** `closes_at` is a future deadline set when the job is published. A background job checks this and closes expired postings. `closed_at` records when the job actually closed, whether via auto-close or manual action. Both are useful for different purposes.

**Cross-schema FKs to `auth.users`.** Both `posted_by` (staff) and `applicant_id` (applicant) reference `auth.users`. Same justification as Profiles — user identity is foundational. The FK on `applicant_id` ensures you can't have an application for a deleted user, and `posted_by` ensures every job has a traceable creator.

**GIN index on `required_skills`.** Supports applicant-facing job search by skill. An applicant searching for "C# jobs" can be served with a JSONB containment query against the job's required skills. Less critical than the profiles GIN index (job search volume is lower than screening volume), but still useful.

**No `application_documents` join table.** The application references a resume via `resume_id` and has an optional `cover_letter_url` inline. These are the only two documents relevant to a specific application (as opposed to the profile's general document collection). If applications ever need arbitrary additional documents, a JSONB column like the profile's `documents` can be added — but the current design is simpler and sufficient.

**Per-module schema.** `recruitment.*` keeps these tables clearly owned by this module. Screening, Matching, and HR Workflows reference applications through shared interfaces — they don't query `recruitment.applications` directly.
