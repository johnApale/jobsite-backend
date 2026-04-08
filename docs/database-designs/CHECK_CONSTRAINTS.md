# CHECK Constraints — Consolidated Reference

Every string enum column across the platform gets a CHECK constraint at the database level. This document is the single reference for all constraints — use it when writing EF Core migrations.

**Enforcement layers:**

1. **Database** — CHECK constraints (this document). Last line of defense. Rejects invalid data on insert/update.
2. **Application** — C# enums in `SharedKernel`. Validates before hitting the DB. Returns friendly 400 errors.
3. **API** — `GET /api/lookups/{type}` endpoints. Returns valid values for frontend dropdowns. Reads from the same C# enums.

Adding a new enum value requires: update the C# enum → add a migration to ALTER the CHECK constraint → deploy both together.

---

## Catalog Database (`catalog` schema)

### tenants

```sql
ALTER TABLE catalog.tenants
  ADD CONSTRAINT chk_tenants_status
  CHECK (status IN ('Provisioning', 'Active', 'Suspended', 'Deactivated'));
```

---

## Tenant Database — Auth Module (`auth` schema)

### users

```sql
ALTER TABLE auth.users
  ADD CONSTRAINT chk_users_role
  CHECK (role IN ('Applicant', 'Recruiter', 'HiringManager', 'Interviewer', 'AgencyAdmin', 'PlatformAdmin'));

ALTER TABLE auth.users
  ADD CONSTRAINT chk_users_status
  CHECK (status IN ('Active', 'Invited', 'Deactivated'));
```

### user_external_logins

```sql
ALTER TABLE auth.user_external_logins
  ADD CONSTRAINT chk_external_logins_provider
  CHECK (provider IN ('Google', 'Apple', 'Facebook'));
```

---

## Tenant Database — Profiles Module (`profiles` schema)

### resumes

```sql
ALTER TABLE profiles.resumes
  ADD CONSTRAINT chk_resumes_file_type
  CHECK (file_type IN ('PDF', 'DOCX'));
```

---

## Tenant Database — Recruitment Module (`recruitment` schema)

### client_companies

```sql
ALTER TABLE recruitment.client_companies
  ADD CONSTRAINT chk_client_companies_status
  CHECK (status IN ('Active', 'Inactive'));

ALTER TABLE recruitment.client_companies
  ADD CONSTRAINT chk_client_companies_industry
  CHECK (industry IS NULL OR industry IN (
    'Technology', 'Healthcare', 'Finance', 'Education', 'Manufacturing',
    'Retail', 'Construction', 'Transportation', 'Hospitality', 'Media',
    'Energy', 'Agriculture', 'RealEstate', 'Legal', 'Consulting',
    'Telecommunications', 'Pharmaceutical', 'Automotive', 'Aerospace',
    'Government', 'NonProfit', 'Other'
  ));
```

### job_postings

```sql
ALTER TABLE recruitment.job_postings
  ADD CONSTRAINT chk_job_postings_location_type
  CHECK (location_type IN ('OnSite', 'Remote', 'Hybrid'));

ALTER TABLE recruitment.job_postings
  ADD CONSTRAINT chk_job_postings_employment_type
  CHECK (employment_type IN ('FullTime', 'PartTime', 'Contract', 'Temporary', 'Internship'));

ALTER TABLE recruitment.job_postings
  ADD CONSTRAINT chk_job_postings_status
  CHECK (status IN ('Draft', 'Published', 'Closed'));
```

### applications

```sql
ALTER TABLE recruitment.applications
  ADD CONSTRAINT chk_applications_status
  CHECK (status IN ('Submitted', 'Screening', 'Assessment', 'Shortlisted', 'FinalInterview', 'Offered', 'Hired', 'Rejected', 'Withdrawn'));

ALTER TABLE recruitment.applications
  ADD CONSTRAINT chk_applications_rejected_at_stage
  CHECK (rejected_at_stage IS NULL OR rejected_at_stage IN ('Screening', 'Assessment', 'Shortlisted', 'FinalInterview', 'Offered'));
```

### job_evaluation_criteria

```sql
ALTER TABLE recruitment.job_evaluation_criteria
  ADD CONSTRAINT chk_criteria_category
  CHECK (category IN ('Skill', 'Experience', 'Certification', 'Education', 'Location', 'Custom'));

ALTER TABLE recruitment.job_evaluation_criteria
  ADD CONSTRAINT chk_criteria_evaluation_method
  CHECK (evaluation_method IN ('ExactMatch', 'RangeMatch', 'SemanticSimilarity'));
```

### job_screening_questions

```sql
ALTER TABLE recruitment.job_screening_questions
  ADD CONSTRAINT chk_questions_type
  CHECK (question_type IN ('FreeText', 'MultipleChoice', 'YesNo'));

ALTER TABLE recruitment.job_screening_questions
  ADD CONSTRAINT chk_questions_timing
  CHECK (timing IN ('AtApplication', 'AfterScreening'));
```

---

## Tenant Database — Screening Module (`screening` schema)

### screening_results

```sql
ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_status
  CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Failed'));

ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_match_strength
  CHECK (match_strength IS NULL OR match_strength IN ('Strong', 'Good', 'Moderate', 'Weak'));

ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_outcome
  CHECK (outcome IS NULL OR outcome IN ('AutoAdvanced', 'AutoRejected', 'ManualReview', 'ManuallyAdvanced', 'ManuallyRejected'));
```

### screening_question_responses

```sql
ALTER TABLE screening.screening_question_responses
  ADD CONSTRAINT chk_question_response_result
  CHECK (score_result IS NULL OR score_result IN ('MeetsRequirement', 'PartialMatch', 'Missing'));
```

---

## Tenant Database — Matching Module (`matching` schema)

### candidate_matches

```sql
ALTER TABLE matching.candidate_matches
  ADD CONSTRAINT chk_matches_match_strength
  CHECK (match_strength IS NULL OR match_strength IN ('Strong', 'Good', 'Moderate', 'Weak'));
```

### shortlists

```sql
ALTER TABLE matching.shortlists
  ADD CONSTRAINT chk_shortlists_status
  CHECK (status IN ('Draft', 'Finalized'));
```

### shortlist_candidates

```sql
ALTER TABLE matching.shortlist_candidates
  ADD CONSTRAINT chk_shortlist_candidates_source
  CHECK (source IN ('Algorithm', 'Manual'));

ALTER TABLE matching.shortlist_candidates
  ADD CONSTRAINT chk_shortlist_candidates_status
  CHECK (status IN ('Pending', 'Approved', 'Rejected'));
```

---

## Tenant Database — HR Workflows Module (`hr_workflows` schema)

### final_interviews

```sql
ALTER TABLE hr_workflows.final_interviews
  ADD CONSTRAINT chk_interviews_status
  CHECK (status IN ('Scheduled', 'InProgress', 'Completed', 'Cancelled', 'NoShow'));

ALTER TABLE hr_workflows.final_interviews
  ADD CONSTRAINT chk_interviews_type
  CHECK (interview_type IN ('InPerson', 'Video', 'Phone'));

ALTER TABLE hr_workflows.final_interviews
  ADD CONSTRAINT chk_interviews_recommendation
  CHECK (overall_recommendation IS NULL OR overall_recommendation IN ('StrongHire', 'Hire', 'NoHire', 'StrongNoHire'));
```

### interview_panelists

```sql
ALTER TABLE hr_workflows.interview_panelists
  ADD CONSTRAINT chk_panelists_recommendation
  CHECK (recommendation IS NULL OR recommendation IN ('StrongHire', 'Hire', 'NoHire', 'StrongNoHire'));
```

### job_offers

```sql
ALTER TABLE hr_workflows.job_offers
  ADD CONSTRAINT chk_offers_status
  CHECK (status IN ('Draft', 'Pending', 'Accepted', 'Declined', 'Withdrawn', 'Expired'));

ALTER TABLE hr_workflows.job_offers
  ADD CONSTRAINT chk_offers_salary_period
  CHECK (salary_period IN ('Annual', 'Monthly', 'Hourly'));

ALTER TABLE hr_workflows.job_offers
  ADD CONSTRAINT chk_offers_employment_type
  CHECK (employment_type IN ('FullTime', 'PartTime', 'Contract', 'Temporary'));
```

---

## AI Service Database (`ai_service` schema)

> **Note:** The AI Service uses a standalone database with tenant ID filtering, not the tenant database. Active tables are `ai_api_logs` and `parsed_resume_cache`. The AI Interview tables below are deferred — constraints will be added when those tables are created.

### ai_api_logs

```sql
ALTER TABLE ai_service.ai_api_logs
  ADD CONSTRAINT chk_api_logs_call_type
  CHECK (call_type IN ('ResumeParsing', 'CriteriaGeneration', 'AssessmentQuestionGeneration', 'ScreeningEvaluation', 'AnswerScoring', 'FeedbackGeneration', 'QuestionGeneration', 'ResponseScoring', 'EvaluationGeneration', 'Transcription'));

ALTER TABLE ai_service.ai_api_logs
  ADD CONSTRAINT chk_api_logs_provider
  CHECK (ai_provider IN ('OpenAI', 'Anthropic', 'AzureOpenAI'));
```

### AI Interview Tables (Deferred)

> **⚠️ DEFERRED** — These constraints will be added when the AI Interview tables are created.

### interview_sessions (deferred)

```sql
ALTER TABLE ai_service.interview_sessions
  ADD CONSTRAINT chk_sessions_status
  CHECK (status IN ('Pending', 'QuestionGeneration', 'InProgress', 'Processing', 'Completed', 'Expired', 'Failed'));
```

### interview_questions (deferred)

```sql
ALTER TABLE ai_service.interview_questions
  ADD CONSTRAINT chk_questions_category
  CHECK (category IN ('Technical', 'Behavioral', 'Situational', 'Experience', 'Communication'));

ALTER TABLE ai_service.interview_questions
  ADD CONSTRAINT chk_questions_difficulty
  CHECK (difficulty IN ('Easy', 'Medium', 'Hard'));
```

### interview_responses (deferred)

```sql
ALTER TABLE ai_service.interview_responses
  ADD CONSTRAINT chk_responses_type
  CHECK (response_type IN ('Text', 'Voice', 'Video', 'Skipped', 'TimedOut'));

ALTER TABLE ai_service.interview_responses
  ADD CONSTRAINT chk_responses_media_type
  CHECK (media_type IS NULL OR media_type IN ('Audio', 'Video'));

ALTER TABLE ai_service.interview_responses
  ADD CONSTRAINT chk_responses_transcription_status
  CHECK (transcription_status IS NULL OR transcription_status IN ('Pending', 'Completed', 'Failed'));
```

### interview_evaluations (deferred)

```sql
ALTER TABLE ai_service.interview_evaluations
  ADD CONSTRAINT chk_evaluations_recommendation
  CHECK (recommendation IN ('StrongAdvance', 'Advance', 'Borderline', 'DoNotAdvance'));
```

---

## Cross-Cutting Validation Rules

These are enforced in application code alongside the CHECK constraints:

**Currency codes:** `varchar(3)` validated against ISO 4217 in code. No CHECK constraint — the list is too long and maintained externally. Applies to: `job_postings.salary_currency`, `job_offers.salary_currency`, `company_settings.default_currency`.

**Timezone:** `varchar(50)` validated against IANA timezone database in code. No CHECK constraint — the list changes with geopolitical updates. Applies to: `company_settings.default_timezone`.

**Email format:** `varchar(254)` validated with regex/library in code. No CHECK constraint — regex in SQL is fragile. Applies to: all email columns.

**URL format:** `varchar(2048)` validated in code. No CHECK constraint. Applies to: all URL columns.

**Score ranges:** `decimal(5,2)` columns for scores (0.00–100.00) can optionally get range constraints:

```sql
-- Optional: enforce score ranges at DB level
ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_overall_score
  CHECK (overall_score IS NULL OR (overall_score >= 0 AND overall_score <= 100));

ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_ai_overall_score
  CHECK (ai_overall_score IS NULL OR (ai_overall_score >= 0 AND ai_overall_score <= 100));

ALTER TABLE screening.screening_results
  ADD CONSTRAINT chk_screening_assessment_score
  CHECK (assessment_score IS NULL OR (assessment_score >= 0 AND assessment_score <= 100));

-- Same pattern for all score columns on candidate_matches, interview_evaluations,
-- response_evaluations, and interview_panelists.rating (1.0-5.0)
```

---

## EF Core Migration Pattern

In EF Core, CHECK constraints are added via `HasCheckConstraint` in the entity configuration:

```csharp
builder.Entity<User>(entity =>
{
    entity.ToTable("users", "auth", t =>
    {
        t.HasCheckConstraint("chk_users_role",
            "role IN ('Applicant', 'Recruiter', 'HiringManager', 'Interviewer', 'AgencyAdmin')");
        t.HasCheckConstraint("chk_users_status",
            "status IN ('Active', 'Invited', 'Deactivated')");
    });
});
```

Keep the C# enum definition in `SharedKernel` as the authoritative source. The CHECK constraint strings should be generated or at least verified against the enum values to prevent drift.
