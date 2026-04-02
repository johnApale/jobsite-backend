# D'Jobsite iConnect — Backend Development Plan

> **Last updated:** 2026-04-01
>
> This document outlines the phased development plan for the D'Jobsite iConnect backend — a modular monolith (C#/.NET 10) with a standalone AI Interview microservice (Python/FastAPI).

---

## Current State Summary

### What's Built

- **Core architecture** — Modular monolith structure, middleware pipeline (CorrelationId, RequestLogging, AppError, TenantResolution), DI composition
- **SharedKernel** — Base classes (`Entity`, `AggregateRoot`), error handling (`AppError`/`AppErrors`), 7 domain events, result types
- **Module scaffolds** — All 8 modules have `Domain`, `Application`, `Infrastructure`, `Api` layer projects
- **Database designs** — All 9 schema design documents completed (Catalog + 8 per-tenant schemas)
- **Conventions & docs** — API, .NET, database, error envelope, testing, and contribution guides
- **AI Interview scaffold** — FastAPI app with health endpoint, project structure, and dependencies defined
- **Test projects** — Unit, Integration, and Architecture test projects created (placeholder tests only)

### What Needs Implementation

- All module endpoints and business logic
- EF Core entity configurations and migrations
- Message broker integration (monolith ↔ AI Interview Service)
- AI Interview Service endpoints, models, services, and AI provider integration
- Full test coverage across test pyramid

---

## Development Phases

### Phase 0 — Foundation & Infrastructure

> Establish the shared infrastructure that all modules depend on.

| #   | Task                             | Module/Area                   | Details                                                                                                                     |
| --- | -------------------------------- | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| 0.1 | **EF Core multi-tenancy setup**  | SharedKernel / Tenancy        | Implement `TenantDbContext` base class, connection string resolution, per-tenant DB provisioning                            |
| 0.2 | **Catalog database schema**      | Tenancy                       | EF Core entities & migration for `tenants` and `tenant_brandings` tables in catalog DB                                      |
| 0.3 | **Tenant resolution middleware** | API                           | Complete `TenantResolutionMiddleware` — subdomain extraction, catalog lookup, Redis caching, DbContext configuration        |
| 0.4 | **Global configuration**         | API                           | Finish `AppSettings.cs` binding — JWT settings, connection strings, broker config, Redis, AI service URL                    |
| 0.5 | **MediatR pipeline**             | SharedKernel                  | Configure MediatR for domain event dispatch, add logging/validation pipeline behaviors                                      |
| 0.6 | **Message broker abstraction**   | SharedKernel / Infrastructure | RabbitMQ / Azure Service Bus publisher/consumer abstraction for integration events                                          |
| 0.7 | **Architecture tests**           | Tests                         | NetArchTest rules — module dependency direction, no cross-module project refs, sealed class enforcement, naming conventions |
| 0.8 | **CI pipeline**                  | DevOps                        | `dotnet build` → `dotnet test` → lint checks; Python `pytest` for AI service                                                |

**Exit Criteria:** Tenant provisioning works end-to-end. A request to `{subdomain}.djobsite.com` resolves to the correct tenant database. Architecture tests enforce module boundaries.

---

### Phase 1 — Auth Module

> Users must authenticate before anything else in the pipeline functions.

| #    | Task                            | Layer                        | Details                                                                                                       |
| ---- | ------------------------------- | ---------------------------- | ------------------------------------------------------------------------------------------------------------- |
| 1.1  | **User entity & configuration** | Domain / Infrastructure      | `User` aggregate root, `UserExternalLogin`, `RefreshToken` entities; EF Core configurations for `auth` schema |
| 1.2  | **EF Core migration**           | Infrastructure               | Initial migration for `auth.users`, `auth.user_external_logins`, `auth.refresh_tokens` with CHECK constraints |
| 1.3  | **Password hashing service**    | Application                  | BCrypt/Argon2 password hashing abstraction                                                                    |
| 1.4  | **JWT service**                 | Application / Infrastructure | Token generation (access + refresh), claims mapping (tenant_id, user_id, role), token validation              |
| 1.5  | **Register endpoint**           | Api                          | `POST /api/v1/auth/register` — email/password registration, email uniqueness validation                       |
| 1.6  | **Login endpoint**              | Api                          | `POST /api/v1/auth/login` — credential validation, JWT issuance, refresh token storage                        |
| 1.7  | **Refresh token endpoint**      | Api                          | `POST /api/v1/auth/refresh` — token rotation, replay detection (token family tracking)                        |
| 1.8  | **OAuth endpoints**             | Api                          | `POST /api/v1/auth/oauth/{provider}` — Google, Apple, Facebook; external login linking                        |
| 1.9  | **Logout endpoint**             | Api                          | `POST /api/v1/auth/logout` — revoke refresh token                                                             |
| 1.10 | **Role-based authorization**    | Api / SharedKernel           | Policy-based auth — Applicant, Recruiter, HiringManager, Interviewer, AgencyAdmin                             |
| 1.11 | **Unit tests**                  | Tests                        | JWT generation/validation, password hashing, refresh token rotation, replay detection                         |
| 1.12 | **Integration tests**           | Tests                        | Full auth flow against Testcontainers PostgreSQL                                                              |

**Exit Criteria:** Users can register, log in, refresh tokens, and authenticate via OAuth. Role-based authorization restricts endpoints. Refresh token replay detection works.

---

### Phase 2 — Admin Module

> Tenant administrators configure company settings and audit trails begin.

| #   | Task                              | Layer                   | Details                                                                                                                                          |
| --- | --------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2.1 | **Company settings entity**       | Domain / Infrastructure | Singleton `CompanySettings` entity with JSONB columns (`screening_config`, `interview_config`, `notification_preferences`, `branding_overrides`) |
| 2.2 | **Audit log entity**              | Domain / Infrastructure | Append-only `AuditLog` entity with denormalized actor data (user_id, email, role snapshot at time of action)                                     |
| 2.3 | **EF Core migration**             | Infrastructure          | `admin.company_settings`, `admin.audit_logs` with appropriate indexes                                                                            |
| 2.4 | **Get/update settings endpoints** | Api                     | `GET/PATCH /api/v1/admin/settings` — partial updates via JSON merge patch                                                                        |
| 2.5 | **Audit log query endpoint**      | Api                     | `GET /api/v1/admin/audit-logs` — cursor-based pagination, filterable by action/actor/date range                                                  |
| 2.6 | **Audit logging service**         | Application             | Intercept domain events and record audit entries automatically                                                                                   |
| 2.7 | **Dashboard stats endpoint**      | Api                     | `GET /api/v1/admin/dashboard` — aggregate pipeline statistics                                                                                    |
| 2.8 | **Tests**                         | Tests                   | Settings CRUD, audit log immutability, pagination                                                                                                |

**Exit Criteria:** Admins can configure tenant settings. All significant actions are audited with denormalized actor snapshots. Dashboard provides pipeline overview.

---

### Phase 3 — Profiles Module

> Applicants create professional profiles that feed into the recruitment pipeline.

| #   | Task                              | Layer                   | Details                                                                                                       |
| --- | --------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------- |
| 3.1 | **Applicant profile entity**      | Domain / Infrastructure | `ApplicantProfile` (shared PK with `auth.users`), JSONB fields for `skills`, `social_links`, `documents`      |
| 3.2 | **Resume entity**                 | Domain / Infrastructure | `Resume` entity with versioning support, `parsed_content` JSONB for pre-parsed data                           |
| 3.3 | **EF Core migration**             | Infrastructure          | `profiles.applicant_profiles`, `profiles.resumes` with cross-schema FK to `auth.users`                        |
| 3.4 | **Profile CRUD endpoints**        | Api                     | `GET/POST/PATCH /api/v1/profiles/me` — applicant self-service profile management                              |
| 3.5 | **Resume upload endpoint**        | Api                     | `POST /api/v1/profiles/me/resumes` — file upload, storage, trigger parse job                                  |
| 3.6 | **Resume parsing background job** | Infrastructure          | Background service to parse uploaded resumes (extract text, skills, experience) and store in `parsed_content` |
| 3.7 | **Tests**                         | Tests                   | Profile creation linked to auth user, resume versioning, parsing pipeline                                     |

**Exit Criteria:** Applicants have profiles linked 1:1 with auth users. Resumes are uploaded, versioned, and parsed asynchronously. Parsed content is queryable.

---

### Phase 4 — Recruitment Module

> Recruiters post jobs and applicants submit applications — the spine of the pipeline.

| #   | Task                          | Layer                   | Details                                                                                                                        |
| --- | ----------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| 4.1 | **Client company entity**     | Domain / Infrastructure | `ClientCompany` for agency-model recruiting                                                                                    |
| 4.2 | **Job posting entity**        | Domain / Infrastructure | `JobPosting` aggregate — title, description, `required_skills` (JSONB), status lifecycle (Draft → Open → Closed → Archived)    |
| 4.3 | **Application entity**        | Domain / Infrastructure | `Application` aggregate — the pipeline spine; status enum tracking full lifecycle; one-app-per-person-per-job constraint       |
| 4.4 | **EF Core migration**         | Infrastructure          | `recruitment.client_companies`, `recruitment.job_postings`, `recruitment.applications` with unique index and CHECK constraints |
| 4.5 | **Job posting CRUD**          | Api                     | `POST/GET/PATCH /api/v1/recruitment/job-postings` — create, list (paginated), update, publish/close                            |
| 4.6 | **Application submission**    | Api                     | `POST /api/v1/recruitment/job-postings/{id}/applications` — validate one-per-person-per-job, attach resume reference           |
| 4.7 | **Application listing**       | Api                     | `GET /api/v1/recruitment/applications` — filterable by status, job posting; cursor-based pagination                            |
| 4.8 | **ApplicationSubmittedEvent** | Domain                  | Publish `ApplicationSubmittedEvent` via MediatR when application is created                                                    |
| 4.9 | **Tests**                     | Tests                   | One-app-per-person-per-job enforcement, status transitions, event publishing                                                   |

**Exit Criteria:** Jobs can be posted and managed. Applicants can apply (one per job). `ApplicationSubmittedEvent` fires and is available for downstream modules.

---

### Phase 5 — Screening Module

> Automated first-pass CV scoring with three-tier routing.

| #   | Task                                  | Layer                   | Details                                                                                                                                                     |
| --- | ------------------------------------- | ----------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1 | **Screening result entity**           | Domain / Infrastructure | `ScreeningResult` (shared PK with applications) — skill score, experience score, quality score, overall score, decision                                     |
| 5.2 | **EF Core migration**                 | Infrastructure          | `screening.screening_results` with cross-schema FK to `recruitment.applications`                                                                            |
| 5.3 | **CV scoring service**                | Application             | Score resumes against job requirements — skill match, experience relevance, quality indicators                                                              |
| 5.4 | **Three-tier routing logic**          | Application             | Auto-advance (score ≥ upper threshold), Manual review (between thresholds), Auto-reject (score ≤ lower threshold); thresholds from `admin.company_settings` |
| 5.5 | **ApplicationSubmittedEvent handler** | Application             | Listen for `ApplicationSubmittedEvent` → trigger screening → publish `CvScreeningCompletedEvent`                                                            |
| 5.6 | **CandidateReadyForInterviewEvent**   | Domain                  | For auto-advanced candidates, publish integration event to message broker                                                                                   |
| 5.7 | **Screening results endpoint**        | Api                     | `GET /api/v1/screening/results` — view scores and decisions; `PATCH` for manual review overrides                                                            |
| 5.8 | **Tests**                             | Tests                   | Scoring algorithm, threshold routing, event chain                                                                                                           |

**Exit Criteria:** Applications are automatically scored. Three-tier routing works with configurable thresholds. Auto-advanced candidates trigger AI Interview via broker event.

---

### Phase 6 — AI Interview Service

> The standalone Python microservice for AI-driven interviews.

| #    | Task                                  | Layer                         | Details                                                                                                                                 |
| ---- | ------------------------------------- | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| 6.1  | **SQLAlchemy models**                 | Infrastructure / DB           | `interview_sessions`, `interview_questions`, `interview_responses`, `response_evaluations`, `interview_evaluations`, `ai_api_logs`      |
| 6.2  | **Alembic migration**                 | Migrations                    | Initial migration for `ai_interview` schema with `tenant_id` on all tables                                                              |
| 6.3  | **RabbitMQ consumer**                 | Infrastructure / Messaging    | Listen for `CandidateReadyForInterviewEvent`, create interview session                                                                  |
| 6.4  | **AI provider integration**           | Infrastructure / AI Providers | OpenAI integration — question generation from job description + resume context                                                          |
| 6.5  | **Question generation service**       | Core / Services               | Generate role-specific interview questions; store in `interview_questions`                                                              |
| 6.6  | **Interview session endpoints**       | Api                           | `GET /api/v1/interviews/{session_id}` — fetch session with questions; `POST /api/v1/interviews/{session_id}/responses` — submit answers |
| 6.7  | **Response processing pipeline**      | Core / Services               | Transcription (if audio/video) → scoring → evaluation generation                                                                        |
| 6.8  | **Response evaluation service**       | Core / Services               | Score individual responses; generate overall `interview_evaluation`                                                                     |
| 6.9  | **InterviewCompletedEvent publisher** | Infrastructure / Messaging    | Publish to broker when evaluation is complete                                                                                           |
| 6.10 | **AI API logging**                    | Infrastructure                | Log all AI provider calls to `ai_api_logs` for cost tracking and debugging                                                              |
| 6.11 | **JWT validation middleware**         | Core                          | Validate tokens issued by monolith Auth module                                                                                          |
| 6.12 | **Tests**                             | Tests                         | pytest — question generation, scoring, event publish/consume, API endpoint tests                                                        |

**Exit Criteria:** AI Interview Service consumes broker events, generates questions via AI, accepts candidate responses, scores them, and publishes completion events back to the monolith.

---

### Phase 7 — Matching Module

> Combine screening and interview scores to rank candidates and build shortlists.

| #   | Task                          | Layer                   | Details                                                                                   |
| --- | ----------------------------- | ----------------------- | ----------------------------------------------------------------------------------------- |
| 7.1 | **Candidate match entity**    | Domain / Infrastructure | `CandidateMatch` — composite score from screening + AI interview, rank                    |
| 7.2 | **Shortlist entities**        | Domain / Infrastructure | `Shortlist` aggregate, `ShortlistCandidate` — per-job-posting shortlists                  |
| 7.3 | **EF Core migration**         | Infrastructure          | `matching.candidate_matches`, `matching.shortlists`, `matching.shortlist_candidates`      |
| 7.4 | **Score aggregation service** | Application             | Consume `CvScreeningCompletedEvent` + `InterviewCompletedEvent` → compute composite score |
| 7.5 | **Shortlist generation**      | Application             | Rank candidates by composite score, generate shortlists for hiring manager review         |
| 7.6 | **CandidateShortlistedEvent** | Domain                  | Publish when candidate is added to shortlist                                              |
| 7.7 | **Shortlist endpoints**       | Api                     | `GET /api/v1/matching/shortlists` — view/manage shortlists; approve/reject candidates     |
| 7.8 | **Tests**                     | Tests                   | Score aggregation, ranking, shortlist generation                                          |

**Exit Criteria:** Screening and AI Interview scores are combined. Candidates are ranked and shortlisted. Hiring managers can review and approve shortlists.

---

### Phase 8 — HR Workflows Module

> Final interviews, panel feedback, and job offers — closing the pipeline.

| #    | Task                                  | Layer                   | Details                                                                                              |
| ---- | ------------------------------------- | ----------------------- | ---------------------------------------------------------------------------------------------------- |
| 8.1  | **Final interview entity**            | Domain / Infrastructure | `FinalInterview` (shared PK with applications), `InterviewPanelist` — scheduling, panel composition  |
| 8.2  | **Job offer entity**                  | Domain / Infrastructure | `JobOffer` (shared PK with applications) — offer lifecycle (Pending → Accepted / Rejected / Expired) |
| 8.3  | **EF Core migration**                 | Infrastructure          | `hr_workflows.final_interviews`, `hr_workflows.interview_panelists`, `hr_workflows.job_offers`       |
| 8.4  | **CandidateShortlistedEvent handler** | Application             | Listen → auto-create final interview placeholder                                                     |
| 8.5  | **Interview scheduling endpoints**    | Api                     | `POST/PATCH /api/v1/hr/interviews` — schedule, assign panelists, set date/time                       |
| 8.6  | **Panel feedback endpoints**          | Api                     | `POST /api/v1/hr/interviews/{id}/feedback` — panelists submit ratings and notes                      |
| 8.7  | **Feedback aggregation**              | Application             | Aggregate panelist scores → recommend hire/no-hire                                                   |
| 8.8  | **Job offer endpoints**               | Api                     | `POST /api/v1/hr/offers` — extend offer; `PATCH` — accept/reject/expire                              |
| 8.9  | **OfferExtendedEvent**                | Domain                  | Publish when offer is extended (for notifications, audit)                                            |
| 8.10 | **Tests**                             | Tests                   | Interview scheduling, feedback aggregation, offer lifecycle                                          |

**Exit Criteria:** Full end-to-end pipeline works — from application submission through screening, AI interview, matching, final interview, to job offer.

---

### Phase 9 — Hardening & Production Readiness

| #    | Task                               | Details                                                                                        |
| ---- | ---------------------------------- | ---------------------------------------------------------------------------------------------- |
| 9.1  | **Integration test suite**         | Testcontainers-based tests for all modules — PostgreSQL, Redis, RabbitMQ                       |
| 9.2  | **Rate limiting**                  | Per-tenant rate limiting on API endpoints (429 responses)                                      |
| 9.3  | **Caching layer**                  | Redis caching for tenant resolution, frequently-read config, job postings                      |
| 9.4  | **Health checks**                  | Deep health checks — DB connectivity, broker connectivity, AI service reachability             |
| 9.5  | **Structured logging**             | Serilog sinks — console (dev), Azure Application Insights / Seq (production)                   |
| 9.6  | **Observability**                  | Distributed tracing (OpenTelemetry), correlation ID propagation across services                |
| 9.7  | **Tenant provisioning automation** | Database creation, schema migration, seed data on tenant registration                          |
| 9.8  | **Security audit**                 | OWASP Top 10 review — SQL injection, XSS, CSRF, broken auth, mass assignment                   |
| 9.9  | **API documentation**              | OpenAPI/Swagger with per-module tags, example requests/responses                               |
| 9.10 | **Performance testing**            | Load testing with k6 — multi-tenant scenarios, concurrent applications                         |
| 9.11 | **Docker & deployment**            | Dockerfile for monolith + AI service; docker-compose for local dev; Kubernetes/Azure manifests |

---

## Module Dependency Graph

```
Phase 0: Foundation ─────────────────────────────────┐
    │                                                 │
Phase 1: Auth ────────────────────────────────────┐   │
    │                                             │   │
Phase 2: Admin ──────────────────────────┐        │   │
    │                                    │        │   │
Phase 3: Profiles ───────────────┐       │        │   │
    │                            │       │        │   │
Phase 4: Recruitment ────┐       │       │        │   │
    │                    │       │       │        │   │
Phase 5: Screening ──────┤       │       │        │   │
    │                    │       │       │        │   │
Phase 6: AI Interview ───┤  (consumes Profiles + Recruitment data via events)
    │                    │       │       │        │   │
Phase 7: Matching ───────┤  (consumes Screening + AI Interview scores)
    │                    │       │       │        │   │
Phase 8: HR Workflows ───┘  (consumes Matching shortlists)
    │
Phase 9: Hardening ──────────────────────────────────┘
```

**Parallelization opportunities:**

- Phases 2 (Admin) and 3 (Profiles) can be developed in parallel after Phase 1
- Phase 6 (AI Interview) can begin once Phase 5 events are defined (stubs/mocks for scoring)

---

## Risk Register

| Risk                                  | Impact                           | Mitigation                                                                                         |
| ------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------------------- |
| AI provider API changes/outages       | AI Interview Service unavailable | Provider abstraction layer; fallback to secondary provider; circuit breaker                        |
| Multi-tenant DB provisioning at scale | Slow tenant onboarding           | Connection pooling; async provisioning pipeline; provisioning queue                                |
| Cross-module event ordering           | Data inconsistency               | Idempotent event handlers; outbox pattern for reliable publishing                                  |
| Resume parsing accuracy               | Poor screening results           | Multiple parsing strategies; human-review fallback; confidence scores                              |
| Token replay attacks                  | Auth compromise                  | Refresh token families; rotation on every use; replay detection revokes family                     |
| Tenant data leakage                   | Security/compliance breach       | Database-per-tenant isolation; middleware-level enforcement; integration tests per tenant boundary |

---

## Tech Stack Reference

| Component          | Technology                                                        |
| ------------------ | ----------------------------------------------------------------- |
| Monolith Runtime   | .NET 10, C#                                                       |
| API Framework      | ASP.NET Core Minimal APIs                                         |
| ORM                | Entity Framework Core                                             |
| Database           | PostgreSQL (per-tenant)                                           |
| Cache              | Redis                                                             |
| Message Broker     | RabbitMQ (dev) / Azure Service Bus (prod)                         |
| AI Service Runtime | Python 3.12+, FastAPI                                             |
| AI Service ORM     | SQLAlchemy 2.0+                                                   |
| AI Providers       | OpenAI (primary)                                                  |
| Auth               | Custom JWT (access + refresh tokens)                              |
| Logging            | Serilog                                                           |
| Testing (.NET)     | xUnit, FluentAssertions, NSubstitute, Testcontainers, NetArchTest |
| Testing (Python)   | pytest                                                            |
| Containerization   | Docker, docker-compose                                            |
