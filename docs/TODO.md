# TODO — Stubs & Deferred Work

> Items that are intentionally stubbed or deferred during initial module implementation.

## Auth Module

### Stubbed

| Item          | Location                                        | Description                                                                               |
| ------------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------- |
| Email Service | `Auth.Infrastructure/Email/StubEmailService.cs` | Logs emails to console instead of sending. Replace with real SMTP/SendGrid in production. |

### Deferred

_(none)_

### Completed

| Item                       | Resolution                                                                                                                                                   |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| EF Core Migration          | `InitialAuthSchema` migration generated and applied. Tables: `auth.users`, `auth.refresh_tokens`, `auth.user_external_logins`.                               |
| Integration Tests          | 29 Auth integration tests added (UserRepository, RefreshTokenRepository, AuthDbContext).                                                                     |
| IUnitOfWork Disambiguation | Resolved via keyed services: `AddKeyedScoped<IUnitOfWork>("auth")` / `("catalog")` with `[FromKeyedServices]`.                                               |
| Rate Limiting              | `"auth"` rate limiting policy (per-IP, 10 req/min) applied to all auth endpoints via `.RequireRateLimiting("auth")`.                                         |
| Account Lockout            | Brute-force protection: `failed_login_attempts` + `locked_until` columns, 5-attempt threshold, 15-min lockout. `AddLockoutAndTokenColumns` migration.        |
| Email Verification Flow    | Token-based email verification on registration. Endpoints: `POST /verify-email`, `POST /resend-verification`. Stub email service logs tokens in development. |
| Password Reset Flow        | Token-based password reset. Endpoints: `POST /forgot-password`, `POST /reset-password`. Reset clears lockout state. 1-hour token expiry.                     |
| OAuth Provider Validation  | Real OAuth validators for Google, Apple, Facebook via `OAuthProviderDispatcher`. `StubOAuthProviderValidator` used in development via conditional DI.        |

---

## Admin Module

### Deferred

_(none)_

### Completed

| Item                        | Resolution                                                                                                                                                        |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Dashboard Stats Endpoint    | `GET /api/v1/admin/dashboard` returns aggregate stats from Recruitment, Screening, and Matching via cross-module readers (`IRecruitmentStatsReader`, `IScreeningStatsReader`, `IMatchingStatsReader`). |
| Platform Admin Controller   | `GET/POST /api/v1/platform/tenants` with list, get, suspend, and reactivate endpoints. `RequirePlatformAdmin` authorization policy. `PlatformAdmin` role added to `UserRole` constants. |
| Company Settings Entity     | Singleton per-tenant `CompanySettings` entity with 6 JSONB settings columns + timezone/currency.                                                                  |
| Tenant Provisioning Wiring  | `TenantProvisioner` now publishes `TenantProvisionedEvent` after successful provisioning, triggering default `CompanySettings` seeding.                           |
| Audit Log Entity            | Append-only `AuditLog` entity with denormalized actor data (survives user deletion).                                                                              |
| EF Core Migration           | `InitialAdminSchema` migration: `admin.company_settings`, `admin.audit_logs` with 4 indexes.                                                                      |
| Settings CRUD Endpoints     | `GET/PATCH /api/v1/admin/settings` with JSON merge patch semantics and FluentValidation.                                                                          |
| Audit Log Query Endpoint    | `GET /api/v1/admin/audit-logs` with cursor-based pagination, action/actor/entity/date filters.                                                                    |
| Domain Event Audit Handlers | 6 domain event handlers for `UserRegistered`, `ApplicationSubmitted`, `CvScreeningCompleted`, `CandidateShortlisted`, `FinalInterviewScheduled`, `OfferExtended`. |
| Tenant Provisioned Handler  | Seeds default `CompanySettings` row (including 4 default evaluation criteria) when tenant is provisioned.                                                         |
| IUnitOfWork Disambiguation  | Keyed service: `AddKeyedScoped<IUnitOfWork>("admin")` with `[FromKeyedServices]`.                                                                                 |

---

## AI Interview Capability (Deferred)

> The AI Interview capability is designed but not yet implemented. It will be built within the AI Service when prioritized. The Assessment stage (recruiter-defined screening questions) covers the post-screening evaluation use case in the current design.

### Deferred

| Item                      | Description                                                                                                                                                                                    |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AI Interview Endpoints    | Session management, question delivery, response submission — to be implemented within the AI Service                                                                                           |
| Broker Consumer/Publisher | `CandidateReadyForInterviewEvent` consumer and `InterviewCompletedEvent` publisher — message broker integration deferred                                                                       |
| Interview DB Tables       | `interview_sessions`, `interview_questions`, `interview_responses`, `response_evaluations`, `interview_evaluations` — schema designed in AI_INTERVIEW_DB_DESIGN.md, Alembic migration deferred |
| AI Interview Settings     | Additional AI-specific fields in `assessment_settings` (`question_mix`, `allowed_response_types`, etc.) — deferred until AI Interview is built                                                 |
| Integration Events        | `CandidateReadyForInterviewEvent` and `InterviewCompletedEvent` event definitions in SharedKernel — deferred                                                                                   |
| Media Transcription       | Speech-to-text for voice/video interview responses — deferred                                                                                                                                  |

---

## Recruitment Module

### Deferred

| Item                                 | Description                                                                                                                                                                                 |
| ------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Repository Integration Tests         | 22 `RecruitmentDbContextTests` exist, but no repository-level integration tests for `IJobPostingRepository`, `IApplicationRepository`, `IClientCompanyRepository`. Requires Testcontainers. |
| Endpoint Tests                       | No `WebApplicationFactory` HTTP pipeline tests for Recruitment endpoints (job posting CRUD, criteria, questions, applications).                                                             |
| AI Criteria Suggestion Contract Test | `AiCriteriaSuggesterClient` tested with mock HTTP handler only — real contract test blocked until AI Service (Phase 6) is operational.                                                      |
| AI Question Suggestion Contract Test | `AiQuestionSuggesterClient` tested with mock HTTP handler only — real contract test blocked until AI Service (Phase 6) is operational.                                                      |

### Completed

| Item                       | Resolution                                                                                                                                                                                                                                                                                                                            |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Client Company Entity      | `ClientCompany` entity with Active/Inactive status, CRUD service, and endpoint.                                                                                                                                                                                                                                                       |
| Job Posting Entity         | `JobPosting` aggregate root with Draft → Published → Closed lifecycle, CRUD service, and endpoints.                                                                                                                                                                                                                                   |
| Application Entity         | `Application` aggregate root with one-per-person-per-job enforcement, withdrawal flow, and `ApplicationSubmittedEvent`.                                                                                                                                                                                                               |
| Evaluation Criteria Entity | `JobEvaluationCriteria` with 6 categories, 3 evaluation methods, weight, and JSONB configuration.                                                                                                                                                                                                                                     |
| Screening Questions Entity | `JobScreeningQuestion` with 3 question types, 2 timing options, expected answer and options JSONB.                                                                                                                                                                                                                                    |
| Job Posting CRUD           | `POST/GET/PATCH /api/v1/recruitment/job-postings` with publish/close lifecycle transitions.                                                                                                                                                                                                                                           |
| Criteria CRUD              | `POST/GET/PATCH/DELETE /api/v1/recruitment/job-postings/{id}/criteria` with AI-assisted suggestions.                                                                                                                                                                                                                                  |
| Questions CRUD             | `POST/GET/PATCH/DELETE /api/v1/recruitment/job-postings/{id}/questions` with feature-gated AI suggestions.                                                                                                                                                                                                                            |
| Application Submission     | `POST /api/v1/recruitment/job-postings/{id}/applications` with resume ownership validation and question answers.                                                                                                                                                                                                                      |
| Unit Tests                 | 189 tests: services (ApplicationService, RecruitmentService, ClientCompanyService, CriteriaService, ScreeningQuestionService), entities, validators (70 across 9 classes), constants (20), AI clients, cross-module boundary services (JobCriteriaReader, ApplicationStatusUpdater, JobScreeningQuestionsReader), service pagination. |
| Integration Tests          | 22 `RecruitmentDbContextTests` covering schema, persistence, CHECK constraints, JSONB, indexes, unique constraints, cascade deletes.                                                                                                                                                                                                  |
| Architecture Tests         | 5 Recruitment layer dependency tests in `LayerDependencyTests.cs`; module isolation already covered by `ModuleIsolationTests.cs`.                                                                                                                                                                                                     |
| IUnitOfWork Disambiguation | Keyed service: `AddKeyedScoped<IUnitOfWork>("recruitment")` with `[FromKeyedServices]`.                                                                                                                                                                                                                                               |
| EF Core Migration          | `InitialRecruitmentSchema` migration generated. Tables: `recruitment.client_companies`, `recruitment.job_postings`, `recruitment.applications`, `recruitment.job_evaluation_criteria`, `recruitment.job_screening_questions` with 10 CHECK constraints and 16 indexes.                                                                |

---

## Profiles Module

### Deferred

| Item                          | Description                                                                                                                                                      |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Profile Completion Evaluation | `ProfileCompletedAt` flag is present but evaluation logic (checking against Admin `ProfileSettings` required fields) is deferred until Admin settings are wired. |
| Cloud File Storage            | `IFileStorage` abstraction exists with `LocalFileStorage` implementation. Azure Blob / S3 implementation deferred to hardening phase.                            |
| Integration Tests             | No Testcontainers tests for `ProfilesDbContext`, `IApplicantProfileRepository`, or `IResumeRepository`. No endpoint tests via `WebApplicationFactory`.           |
| MassTransit Consumer E2E      | No end-to-end test with Testcontainers RabbitMQ for resume upload → parse pipeline.                                                                              |

### Completed

| Item                        | Resolution                                                                                                                                                                                                |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Applicant Profile Entity    | `ApplicantProfile` aggregate root with shared PK to `auth.users`, JSONB fields for skills/social links/documents.                                                                                         |
| Resume Entity               | `Resume` entity with versioning (`is_latest`), parsing state, basic + AI parsed content JSONB columns.                                                                                                    |
| Profile CRUD Endpoints      | `GET/POST/PATCH /api/v1/profiles/me` with JSON merge patch semantics and FluentValidation.                                                                                                                |
| Resume Endpoints            | `POST/GET /api/v1/profiles/me/resumes`, `GET /api/v1/profiles/me/resumes/{id}` — upload, list, get by ID.                                                                                                 |
| Resume Upload + Parsing     | File storage abstraction, MassTransit consumer for async parsing, basic text extraction (PdfPig + OpenXml), keyword skill matching.                                                                       |
| AI Resume Parsing           | `AiResumeParserClient` with resilient HTTP (timeout/retry/circuit breaker), graceful null fallback when AI Service unavailable.                                                                           |
| UserRegisteredEvent Handler | Auto-creates empty `ApplicantProfile` when user registers with Applicant role; skips other roles, idempotent.                                                                                             |
| Resume Parse Recovery       | `ResumeParseRecoveryService` (BackgroundService) retries failed/unparsed resumes on startup.                                                                                                              |
| Unit Tests                  | 83 tests: ProfileService, ResumeService, validators, event handler, constants, AI parser client, ResumeUploadedConsumer (8), BasicResumeParser (8), LocalFileStorage (6), ResumeParseRecoveryService (5). |
| Architecture Tests          | 5 Profiles layer dependency tests added to `LayerDependencyTests.cs`; module isolation already covered by `ModuleIsolationTests.cs`.                                                                      |
| IUnitOfWork Disambiguation  | Keyed service: `AddKeyedScoped<IUnitOfWork>("profiles")` with `[FromKeyedServices]`.                                                                                                                      |
| EF Core Migration           | `InitialProfilesSchema` migration generated. Tables: `profiles.applicant_profiles`, `profiles.resumes` with 1 CHECK constraint and 4 indexes.                                                             |

---

## Matching Module

### Deferred

| Item                        | Description                                                                                                                  |
| --------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Integration Tests           | No Testcontainers tests for `MatchingDbContext`, `ICandidateMatchRepository`, or `IShortlistRepository`. No endpoint tests.  |
| Auto-Generate Shortlist     | `auto_generate_shortlist` setting in `MatchingSettings` is read but not yet wired to trigger automatic shortlist generation. |
| Shortlist Approval Workflow | No hiring manager approval/rejection of individual shortlist candidates — only full shortlist finalization.                  |

### Completed

| Item                              | Resolution                                                                                                                                                                        |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CandidateMatch Entity             | `CandidateMatch` with shared PK (ApplicationId), screening/assessment/composite scores, rank, MatchStrength.                                                                      |
| Shortlist Entity                  | `Shortlist` aggregate root with `ShortlistCandidate` collection, Draft/Finalized lifecycle.                                                                                       |
| Score Aggregation Service         | Weighted composite score computation with tenant-configurable screening/assessment weights from `MatchingSettings`.                                                               |
| CvScreeningCompletedEvent Handler | Consumes event from Screening → creates `CandidateMatch` with idempotency check. Uses cross-module readers for application data.                                                  |
| AssessmentCompletedEvent Handler  | Updates existing `CandidateMatch` with assessment score, recomputes composite.                                                                                                    |
| Shortlist Generation              | Top-N candidates by composite score, Algorithm source attribution, ranked candidates.                                                                                             |
| Shortlist Management              | Manual candidate add/remove on draft shortlists, soft-delete, duplicate detection.                                                                                                |
| Shortlist Finalization            | Status lock, `CandidateShortlistedEvent` dispatch per candidate, application status update to "Shortlisted" via `IApplicationStatusUpdater`.                                      |
| Cross-Module Readers              | `IScreeningScoreReader` (SharedKernel → Screening.Infrastructure), `IApplicationDataReader` (SharedKernel → Recruitment.Infrastructure).                                          |
| API Endpoints                     | 7 endpoints: GET match, GET matches, POST generate shortlist, GET shortlist, GET shortlists, POST add candidate, DELETE remove candidate, POST finalize.                          |
| Unit Tests                        | 39 tests: constants (4), score aggregation (8), matching service (5), shortlist service (11), event handlers (7), validators (4).                                                 |
| Architecture Tests                | Module isolation and naming convention tests updated to reference Matching types.                                                                                                 |
| IUnitOfWork Disambiguation        | Keyed service: `AddKeyedScoped<IUnitOfWork>("matching")` with `[FromKeyedServices]`.                                                                                              |
| EF Core Migration                 | `InitialMatchingSchema` migration generated. Tables: `matching.candidate_matches`, `matching.shortlists`, `matching.shortlist_candidates` with 3 CHECK constraints and 8 indexes. |

---

## Testing — Deferred Until AI Service Is Running

> These tests require the AI Service (Python/FastAPI) to be operational. They cannot be written as unit tests with mocks — they validate the real integration contracts, end-to-end pipelines, and the AI Service's own behavior.

### AI Service Contract Tests (C# → Python)

| Item                                       | Description                                                                                                                                                               |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AI Scoring Client Contract Test            | `AiScoringClient.EvaluateAsync` against real `/api/v1/scoring/evaluate` — verify request/response JSON shape, snake_case contract, and score ranges (0–100).              |
| AI Answer Scoring Client Contract Test     | `AiAnswerScoringClient.ScoreAnswersAsync` against real `/api/v1/scoring/answers` — verify free-text answer scores are returned with `ScoreResult` and `ScoreReasoning`.   |
| AI Candidate Feedback Client Contract Test | `AiCandidateFeedbackClient.GenerateFeedbackAsync` against real `/api/v1/feedback/generate` — verify transparency levels (Summary vs Detailed) produce different output.   |
| AI Resume Parser Client Contract Test      | `AiResumeParserClient.ParseAsync` against real `/api/v1/parsing/resume` — verify parsed skills, experience, and education are returned in expected structure.             |
| AI Criteria Suggester Client Contract Test | `AiCriteriaSuggesterClient.SuggestAsync` against real `/api/v1/suggestions/criteria` — verify suggestions contain valid `CriteriaCategory` and `EvaluationMethod` values. |
| AI Question Suggester Client Contract Test | `AiQuestionSuggesterClient.SuggestAsync` against real `/api/v1/suggestions/questions` — verify suggestions contain valid `QuestionType` and `QuestionTiming` values.      |

### End-to-End Screening Pipeline

| Item                                   | Description                                                                                                                                                       |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Full Screening Pipeline E2E            | Submit application → deterministic scoring + AI scoring → three-tier routing → assessment submission → candidate feedback generation. Full pipeline with real AI. |
| AI Scoring vs Deterministic Comparison | Verify `AiOverallScore` and `OverallScore` are independently populated and can differ. Validate both breakdowns are stored correctly in JSONB.                    |
| Candidate Feedback E2E                 | Enable transparency → run screening → verify `CandidateFeedback` JSONB is populated with AI-generated text at the configured transparency level.                  |

### AI Interview Service Integration (Broker)

| Item                                | Description                                                                                                                                                      |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CandidateReadyForInterviewEvent E2E | Publish event via MassTransit → AI Service consumes from RabbitMQ/Azure Service Bus → verify interview session created in AI Service DB.                         |
| InterviewCompletedEvent E2E         | AI Service publishes completion event → .NET monolith consumes → verify application status updated and `InterviewCompletedEvent` audit log entry created.        |
| Broker Serialization Contract Test  | Verify `CandidateReadyForInterviewEvent` and `InterviewCompletedEvent` snake_case JSON matches Python Pydantic model expectations. Uses Testcontainers RabbitMQ. |

### AI Service pytest Suite (`ai-service/tests/`)

### Completed

| Item                   | Resolution                                                                                                                                  |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| FastAPI Endpoint Tests | 11 test cases in `test_api.py` covering all 6 endpoints (happy path 200 + validation 422). Auth overridden via dependency injection.        |
| Auth Tests             | 5 tests in `test_auth.py` — valid/expired/invalid JWT, missing tenant_id, missing role claims.                                              |
| Error Envelope Tests   | 5 tests in `test_errors.py` — status codes, envelope format, request_id propagation, health endpoint, error codes.                          |
| Service Unit Tests     | 16 tests in `test_services.py` — AiLoggingService (4), ResumeService (4), CriteriaService (2), AssessmentService (2), ScreeningService (4). |
| Resume Caching Tests   | 4 tests in `test_caching.py` — cache hit/miss, 30-day TTL, cross-tenant cache sharing.                                                      |
| Schema Tests           | 6 tests in `test_schemas.py` — exclude_none behavior, snake_case serialization, PascalCase enum values, optional field defaults.            |

### Deferred

| Item                          | Description                                                                                                                              |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| SQLAlchemy Model Tests        | Verify Alembic migrations create correct tables (`ai_api_logs`, `parsed_resume_cache`) against Testcontainers PostgreSQL.                |
| AI Provider Integration Tests | Test AI provider abstraction (OpenAI/Azure OpenAI) with mock responses — verify prompt construction, token limits, and response parsing. |
| Message Broker Consumer Tests | Test `CandidateReadyForInterviewEvent` consumer creates interview session with correct tenant filtering and question generation.         |
| Tenant ID Filtering Tests     | Verify shared-database tenant isolation — write via tenant A, query via tenant B → zero results.                                         |
