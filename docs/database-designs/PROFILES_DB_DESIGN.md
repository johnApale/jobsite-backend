# Profiles Module — Database Design

Applicant profile data, separate from auth. This is the "professional identity" of a candidate — their summary, skills, contact details, resumes, and documents. Auth owns the login credentials; Profiles owns everything a recruiter or hiring manager needs to evaluate a person.

Read-heavy tables. Recruitment, Screening, and Matching all query this module's data through shared interfaces. No outbound domain events — this is a passive data module that other modules consume.

---

## Tables

### applicant_profiles

One-to-one with `auth.users` where the user's role is `Applicant`. Staff users (Recruiter, HiringManager, Interviewer, AgencyAdmin) don't have profiles — they don't go through the hiring pipeline. Created when an applicant registers (either email/password or OAuth) and fills out their profile.

Uses a shared primary key pattern — `user_id` is both the PK and a logical reference to `auth.users` (no DB-level FK constraint). Same approach as `tenant_brandings` in the catalog DB.

| Column               | Type         | Constraints             | Description                                                                                                          |
| -------------------- | ------------ | ----------------------- | -------------------------------------------------------------------------------------------------------------------- |
| user_id              | uuid         | PK, ref → auth.users.id | Shared key — also the primary key. One profile per applicant                                                         |
| phone                | varchar(20)  | nullable                | Contact phone number. Not required on initial registration                                                           |
| city                 | varchar(100) | nullable                | Current city. Used for location-based job matching                                                                   |
| country              | varchar(100) | nullable                | Current country                                                                                                      |
| headline             | varchar(200) | nullable                | Short professional headline (e.g., "Senior .NET Developer with 8 years experience")                                  |
| summary              | text         | nullable                | Professional summary / bio. Free-form text, no length limit enforced at DB level                                     |
| skills               | jsonb        | NOT NULL, DEFAULT '[]'  | Array of skill objects with proficiency and years. Queryable with GIN index. See Skills Format below                 |
| documents            | jsonb        | NOT NULL, DEFAULT '[]'  | Additional documents: cover letters, certifications, portfolios. See Documents Format below                          |
| social_links         | jsonb        | NOT NULL, DEFAULT '{}'  | Social media and professional profile URLs. See Social Links Format below                                            |
| profile_completed_at | timestamp    | nullable                | Set when the applicant has filled the tenant's required fields. Used to filter incomplete profiles from job matching |
| created_at           | timestamp    | NOT NULL                |                                                                                                                      |
| updated_at           | timestamp    | NOT NULL                | Auto-set on modification                                                                                             |

**Indexes:**

| Name                     | Columns       | Type       | Purpose                                                                             |
| ------------------------ | ------------- | ---------- | ----------------------------------------------------------------------------------- |
| ix_profiles_skills       | skills        | GIN        | JSONB containment queries for skill matching (e.g., `skills @> '[{"name": "C#"}]'`) |
| ix_profiles_city_country | city, country | Non-unique | Location-based job matching and filtering                                           |

No index on `user_id` — it's the primary key.

---

### resumes

Every resume an applicant uploads. Parsed once on upload, stored permanently. The applicant can upload multiple versions over time — `is_latest` marks the current one. Applications reference a specific resume version, so updating the profile resume doesn't affect in-flight applications.

| Column            | Type          | Constraints                   | Description                                                                                                               |
| ----------------- | ------------- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| id                | uuid          | PK                            |                                                                                                                           |
| user_id           | uuid          | NOT NULL, ref → auth.users.id | The applicant who owns this resume                                                                                        |
| file_url          | varchar(2048) | NOT NULL                      | CDN/blob storage URL to the uploaded file (PDF/DOCX)                                                                      |
| original_filename | varchar(500)  | NOT NULL                      | Original filename at upload time. Display purposes                                                                        |
| file_size_bytes   | integer       | nullable                      | File size for display and storage quota enforcement                                                                       |
| file_type         | varchar(20)   | NOT NULL                      | Enum: `PDF`, `DOCX`. Determines which parser to use                                                                       |
| is_latest         | boolean       | NOT NULL, DEFAULT true        | Whether this is the applicant's current resume. Only one per user should be true at a time — enforced in application code |
| is_parsed         | boolean       | NOT NULL, DEFAULT false       | Whether parsing has completed. False on upload, true after background job finishes                                        |
| parsed_text       | text          | nullable                      | Full text extracted from the resume. Set by background parser. NULL until parsed                                          |
| extracted_skills  | jsonb         | nullable                      | Skills parsed from the resume. See Extracted Skills Format below. NULL until parsed                                       |
| parsed_at         | timestamp     | nullable                      | When parsing completed. NULL until parsed                                                                                 |
| parse_error       | varchar(500)  | nullable                      | If parsing failed: what went wrong (e.g., "Password-protected PDF", "Corrupted file"). NULL on success                    |
| uploaded_at       | timestamp     | NOT NULL                      | When the applicant uploaded this file                                                                                     |
| created_at        | timestamp     | NOT NULL                      |                                                                                                                           |

**Indexes:**

| Name                   | Columns            | Type       | Purpose                                                 |
| ---------------------- | ------------------ | ---------- | ------------------------------------------------------- |
| ix_resumes_user_id     | user_id            | Non-unique | "All resumes for this applicant" and finding the latest |
| ix_resumes_is_parsed   | is_parsed          | Non-unique | Background parser queue: find unparsed resumes          |
| ix_resumes_user_latest | user_id, is_latest | Non-unique | Quick lookup for the applicant's current resume         |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS profiles;
```

All Profiles module tables live under the `profiles` schema. Other modules read profile data through a shared interface in code — not by querying `profiles.*` tables directly.

---

## Relationships

```
auth.users ||--o| applicant_profiles : "has (optional, one-to-one, applicants only)"
auth.users ||--o{ resumes : "has many (resume history)"
```

Both tables reference `auth.users` across schemas as logical references (no DB-level FK constraints). Integrity is enforced at the application layer via SharedKernel interfaces — the Auth module guarantees user existence before downstream modules create related records.

---

## Resume Upload & Parse Flow

```
1. Applicant uploads a new resume
2. File uploaded to blob storage → file_url generated
3. New resumes row created:
   - is_latest = true, is_parsed = false
   - parsed_text = NULL, extracted_skills = NULL
4. Previous latest resume (if any) updated: is_latest = false
5. Background parser job picks up unparsed resumes (is_parsed = false):
   a. Download file from file_url
   b. Extract text based on file_type (PDF → iText/pdftotext, DOCX → OpenXml)
   c. Run skill extraction on parsed text (AI/NLP or rule-based)
   d. Store parsed_text, extracted_skills, parsed_at
   e. Set is_parsed = true
   f. On error: set parse_error, leave is_parsed = false for retry or manual review
6. Resume is now available for screening — no re-parsing needed
```

**Parse once, use everywhere.** When a Screening evaluation runs, it reads the pre-parsed `parsed_text` and `extracted_skills` from the resume record. No download, no parsing, no duplicate storage. If the applicant submits the same resume to ten jobs, the text is parsed once and the ten screening results all reference the same resume record.

---

## How Applications Reference Resumes

When an applicant submits a job application, the `recruitment.applications` table stores a `resume_id` FK pointing to `profiles.resumes`. This references the specific resume version they submitted with — not "whatever their latest resume is."

If the applicant uploads a new resume after applying, the new resume becomes `is_latest = true` on their profile, but the in-flight application still points to the old resume record. Screening evaluates the exact version submitted.

---

## Extracted Skills Format

Skills parsed from the resume by the background parser. Stored on the `resumes` row so they're computed once per upload.

```json
[
  { "name": ".NET", "years": 5, "confidence": 0.95 },
  { "name": "SQL Server", "years": 3, "confidence": 0.87 },
  { "name": "Team Leadership", "confidence": 0.72 }
]
```

**`name`**: Skill name as extracted from the resume text.

**`years`** (optional): Years of experience inferred from the resume context. May not always be extractable.

**`confidence`**: How confident the parser is in this extraction (0.0–1.0). Lower confidence skills might be inferred from context rather than explicitly stated.

These extracted skills may differ from the applicant's self-reported `skills` on their profile. The Screening module has access to both and decides how to reconcile them in its scoring algorithm.

---

## Skills Format

Skills are stored as a JSONB array on `applicant_profiles`. Each skill has a name, an optional proficiency level, and optional years of experience with that specific skill. This is the applicant's self-reported data — distinct from the parser-extracted skills on the resume.

```json
[
  { "name": "C#", "level": "Advanced", "years": 7 },
  { "name": "PostgreSQL", "level": "Intermediate", "years": 3 },
  { "name": "Docker", "level": "Beginner", "years": 1 },
  { "name": "Project Management", "level": "Advanced" }
]
```

**`name`** (required): The skill name. Free-text — no master skill list. Normalization (matching "C#" to "CSharp" to "C Sharp") is an application-level concern, not a database one.

**`level`** (optional): Self-reported proficiency. Enum in application code: `Beginner`, `Intermediate`, `Advanced`, `Expert`. Not enforced at DB level — it's informational and subjective.

**`years`** (optional): Years of experience with this specific skill. Used by Screening for per-skill matching against job requirements (e.g., job requires "C# with 5+ years", applicant has 7 → strong match).

**Why JSONB instead of a normalized skills table?**

- Skills are always read and written as a set — you never update one skill in isolation
- The GIN index supports containment queries (`@>`) which is all the Screening module needs for filtering ("has C#?")
- Detailed scoring (per-skill years vs requirements) is done in application code after loading the JSONB — not in SQL
- Avoids a `skills` table + `applicant_skills` join table + proficiency column, which triples the schema complexity for a feature that doesn't need relational joins
- If skill analytics become important later (e.g., "most common skills across all applicants"), a normalized table can be extracted — but that's a reporting concern, not a transactional one

---

## Social Links Format

Social media and professional profile URLs. Stored as a JSONB object (key-value pairs) so new platforms can be added without schema changes or migrations.

```json
{
  "linkedin": "https://linkedin.com/in/johndoe",
  "github": "https://github.com/johndoe",
  "portfolio": "https://johndoe.dev",
  "twitter": "https://twitter.com/johndoe",
  "behance": "https://behance.net/johndoe"
}
```

Keys are lowercase platform identifiers. Values are full URLs. No fixed list of keys — the application code (and tenant configuration, see below) determines which platforms are shown in the UI and which are required.

**Why JSONB instead of columns or a separate table?**

- The list of platforms will grow unpredictably (LinkedIn today, Bluesky tomorrow)
- Adding a column per platform means a migration every time a new platform is relevant
- A separate `social_links` table with `platform` + `url` rows adds a join for data that's always read as a set
- JSONB lets tenants configure which platforms they care about (via Admin settings) without touching the schema

---

## Documents Format

Additional documents beyond resumes. Stored as a JSONB array so new document types can be added without schema changes.

```json
[
  {
    "type": "CoverLetter",
    "url": "https://storage.djobsite.com/tenant-abc/docs/cl-123.pdf",
    "filename": "cover_letter_2025.pdf",
    "uploaded_at": "2025-03-15T10:30:00Z"
  },
  {
    "type": "Certification",
    "url": "https://storage.djobsite.com/tenant-abc/docs/cert-456.pdf",
    "filename": "aws_solutions_architect.pdf",
    "uploaded_at": "2025-02-20T14:00:00Z"
  }
]
```

**`type`**: Enum in application code — `CoverLetter`, `Certification`, `Portfolio`, `Reference`, `Other`. Extensible without migrations.

**`url`**: CDN/blob storage URL. The file itself is in Azure Blob / S3, not in the database.

**`filename`**: Original filename for display purposes.

**`uploaded_at`**: When the document was added. Useful for showing "most recent" and for cleanup of orphaned blob storage files.

---

## Profile Completion & Tenant-Configurable Fields

`profile_completed_at` is set when the applicant has filled out the fields required by their tenant. The required fields are **not hardcoded in the Profiles module** — they're configured per-tenant in the Admin module's `CompanySettings` table.

Example configuration (stored in Admin's `CompanySettings` as JSONB):

```json
{
  "required_profile_fields": ["phone", "skills"],
  "required_social_links": ["linkedin"],
  "required_documents": ["CoverLetter"],
  "minimum_skills_count": 3,
  "resume_required": true
}
```

This means:

- Acme requires phone, 3+ skills, LinkedIn, a cover letter, and at least one resume uploaded
- Beta might only require a resume and 1 skill
- The Profiles module validates against whatever the tenant has configured

The Profiles module reads the tenant's field requirements at validation time. The `profile_completed_at` timestamp is set when all of that tenant's requirements are met. Incomplete profiles can still exist — the applicant just won't pass the completion check.

---

## Scoring Optimization

The profile schema is designed to support the Screening and Matching scoring pipeline efficiently. Here's how the modules use the data:

**Pre-filter (SQL, fast):** The Screening module uses indexed columns and GIN queries to narrow the candidate pool before doing expensive scoring:

- `skills @> '[{"name": "C#"}]'` — GIN index, checks skill presence without parsing
- `city = {job_city}` — indexed, location match
- Has a latest parsed resume — join to `resumes` where `is_latest = true AND is_parsed = true`
- `profile_completed_at IS NOT NULL` — incomplete profiles deprioritized

**Detailed scoring (application code, per-candidate):** After pre-filtering, the Screening and Matching services load the full profile and resume data, then run weighted scoring algorithms in code:

- Per-skill matching: compare each skill's `name`, `level`, and `years` against job requirements
- Skill coverage: percentage of required skills the applicant has
- Experience weighting: per-skill years vs job's per-skill requirements
- Resume analysis: read `parsed_text` and `extracted_skills` from the resume record — no re-parsing

**Why scoring lives in code, not SQL:**

- Scoring algorithms are business logic that changes frequently (weight adjustments, new factors)
- Multi-factor scoring with weighting doesn't map cleanly to SQL — it's cleaner in C#
- The pre-filter in SQL reduces the set to a manageable size (dozens to low hundreds per job), so the in-code scoring isn't a performance concern
- Different tenants might eventually want different scoring weights — that's an application config, not a schema change

---

## Design Decisions

**Shared primary key for profiles (`user_id` as PK + FK).** Enforces one-to-one at the database level, avoids a redundant `id` column, and makes joins trivial. Same pattern as `tenant_brandings` in the catalog.

**Dedicated `resumes` table instead of a `resume_url` column on profiles.** Resumes are parsed once on upload and the results are reused across all applications that reference that resume. A column on the profile would mean: no resume history (overwrite on re-upload), no place to store parsed data, and either re-parsing on every screening or duplicating parsed text across screening results. The table solves all of this cleanly.

**Parse once, use everywhere.** Resume parsing (PDF text extraction, skill extraction via AI/NLP) is expensive. Doing it once on upload and storing the results on the resume record means ten applications with the same resume don't trigger ten parse jobs. Screening reads pre-parsed data — fast and consistent.

**`is_latest` flag instead of a "current_resume_id" FK on profiles.** Both work. The flag approach keeps the resumes table self-contained and avoids a circular dependency (profile → resume → user, vs profile → user, resume → user with flag). The slight downside is enforcing "only one is_latest per user" requires application code, not a database constraint. A partial unique index (`CREATE UNIQUE INDEX ... WHERE is_latest = true`) could enforce this at the DB level if desired.

**`is_parsed` for background processing.** Parsing happens asynchronously after upload. The applicant doesn't wait for parsing to complete before their resume is "uploaded." The background job picks up unparsed resumes and processes them. If parsing fails, `parse_error` records why, and the resume can be retried or flagged for the applicant to re-upload.

**No `updated_at` on resumes.** Resumes are immutable after parsing. They're uploaded, parsed once, and then only read. The only state change is `is_latest` toggling to false when a new version is uploaded — and that's tracked by the new resume's `uploaded_at`. If a re-parse is ever needed, `parsed_at` updates — but that's rare enough to not warrant a general `updated_at`.

**Cross-schema references to `auth.users` (no DB-level FK constraints).** Both tables reference Auth. Integrity is enforced at the application layer — the Auth module guarantees user existence via domain events before Profiles creates related records.

**`resume_url` removed from `applicant_profiles`.** No longer needed — the latest resume is found via the `resumes` table (`is_latest = true`). The profile stays focused on the applicant's self-reported data.

**Social links as JSONB object, not columns.** Platforms come and go. A JSONB object with string keys means adding Bluesky or Mastodon support is a UI change, not a migration. The Admin module controls which platforms each tenant requires.

**Per-skill `years` in the skills JSONB.** Critical for scoring. "5 years of C#" is a fundamentally different signal than knowing C# with unspecified experience. Per-skill years let the Screening module do meaningful requirement matching.

**Tenant-configurable required fields via Admin, not Profiles.** The Profiles module doesn't decide what's required — the tenant admin does. This keeps Profiles as a pure data store.

**No outbound domain events.** Profiles is passive. It doesn't trigger any pipeline actions. Other modules read from it when they need applicant data. This keeps the module simple and avoids unnecessary coupling.
