# TODO — Stubs & Deferred Work

> Items that are intentionally stubbed or deferred during initial module implementation.

## Auth Module

### Stubbed

| Item                      | Location                                                     | Description                                                                                             |
| ------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| OAuth Provider Validation | `Auth.Infrastructure/Security/StubOAuthProviderValidator.cs` | Returns deterministic stub data instead of calling Google/Apple/Facebook APIs. Logs warning in non-dev. |

### Deferred

| Item                    | Description                                                                                      |
| ----------------------- | ------------------------------------------------------------------------------------------------ |
| Email Verification Flow | `email_verified` flag is set but no verification email is sent. Needs email service integration. |
| Password Reset Flow     | No forgot-password / reset-password endpoints yet.                                               |
| Account Lockout         | No brute-force protection (failed attempt tracking / lockout).                                   |
| Rate Limiting           | No per-endpoint rate limiting on auth endpoints.                                                 |

### Completed

| Item                       | Resolution                                                                                                                     |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| EF Core Migration          | `InitialAuthSchema` migration generated and applied. Tables: `auth.users`, `auth.refresh_tokens`, `auth.user_external_logins`. |
| Integration Tests          | 29 Auth integration tests added (UserRepository, RefreshTokenRepository, AuthDbContext).                                       |
| IUnitOfWork Disambiguation | Resolved via keyed services: `AddKeyedScoped<IUnitOfWork>("auth")` / `("catalog")` with `[FromKeyedServices]`.                 |

---

## Admin Module

### Deferred

| Item                       | Description                                                                                                         |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Dashboard Stats Endpoint   | `GET /api/v1/admin/dashboard` — aggregate pipeline statistics. Deferred until Recruitment/Screening modules exist.  |
| Platform Admin Controller  | System-wide operations against the Catalog DB (e.g., tenant listing). Not part of per-tenant admin.                 |
| Tenant Provisioning Wiring | `TenantProvisionedEvent` handler exists but tenant service does not yet publish it. Needs wiring in Tenancy module. |

### Completed

| Item                        | Resolution                                                                                                                                                   |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Company Settings Entity     | Singleton per-tenant `CompanySettings` entity with 6 JSONB settings columns + timezone/currency.                                                             |
| Audit Log Entity            | Append-only `AuditLog` entity with denormalized actor data (survives user deletion).                                                                         |
| EF Core Migration           | `InitialAdminSchema` migration: `admin.company_settings`, `admin.audit_logs` with 4 indexes.                                                                 |
| Settings CRUD Endpoints     | `GET/PATCH /api/v1/admin/settings` with JSON merge patch semantics and FluentValidation.                                                                     |
| Audit Log Query Endpoint    | `GET /api/v1/admin/audit-logs` with cursor-based pagination, action/actor/entity/date filters.                                                               |
| Domain Event Audit Handlers | 6 MediatR handlers for `UserRegistered`, `ApplicationSubmitted`, `CvScreeningCompleted`, `CandidateShortlisted`, `FinalInterviewScheduled`, `OfferExtended`. |
| Tenant Provisioned Handler  | Seeds default `CompanySettings` row (including 4 default evaluation criteria) when tenant is provisioned.                                                    |
| IUnitOfWork Disambiguation  | Keyed service: `AddKeyedScoped<IUnitOfWork>("admin")` with `[FromKeyedServices]`.                                                                            |

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

## Profiles Module

### Deferred

| Item                          | Description                                                                                                                                                       |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| EF Core Migration             | `InitialProfilesSchema` migration not yet generated — requires running PostgreSQL with tenant DB provisioning.                                                    |
| Profile Completion Evaluation | `ProfileCompletedAt` flag is present but evaluation logic (checking against Admin `ProfileSettings` required fields) is deferred until Admin settings are wired.  |
| Cloud File Storage            | `IFileStorage` abstraction exists with `LocalFileStorage` implementation. Azure Blob / S3 implementation deferred to hardening phase.                             |
| Integration Tests             | Profiles integration tests (repository CRUD with Testcontainers) not yet created. Unit tests provide coverage for services, validators, and event handlers.       |

### Completed

| Item                        | Resolution                                                                                                                                                  |
| --------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Applicant Profile Entity    | `ApplicantProfile` aggregate root with shared PK to `auth.users`, JSONB fields for skills/social links/documents.                                           |
| Resume Entity               | `Resume` entity with versioning (`is_latest`), parsing state, basic + AI parsed content JSONB columns.                                                      |
| Profile CRUD Endpoints      | `GET/POST/PATCH /api/v1/profiles/me` with JSON merge patch semantics and FluentValidation.                                                                  |
| Resume Endpoints             | `POST/GET /api/v1/profiles/me/resumes`, `GET /api/v1/profiles/me/resumes/{id}` — upload, list, get by ID.                                                   |
| Resume Upload + Parsing      | File storage abstraction, MassTransit consumer for async parsing, basic text extraction (PdfPig + OpenXml), keyword skill matching.                          |
| AI Resume Parsing            | `AiResumeParserClient` with resilient HTTP (timeout/retry/circuit breaker), graceful null fallback when AI Service unavailable.                              |
| UserRegisteredEvent Handler  | Auto-creates empty `ApplicantProfile` when user registers with Applicant role; skips other roles, idempotent.                                               |
| Resume Parse Recovery        | `ResumeParseRecoveryService` (BackgroundService) retries failed/unparsed resumes on startup.                                                                |
| Unit Tests                   | 56 tests: ProfileService, ResumeService, validators, event handler, constants, AI parser client.                                                            |
| Architecture Tests           | 5 Profiles layer dependency tests added to `LayerDependencyTests.cs`; module isolation already covered by `ModuleIsolationTests.cs`.                        |
| IUnitOfWork Disambiguation   | Keyed service: `AddKeyedScoped<IUnitOfWork>("profiles")` with `[FromKeyedServices]`.                                                                        |
