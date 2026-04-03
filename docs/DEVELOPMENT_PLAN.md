# D'Jobsite iConnect — Backend Development Plan

> **Last updated:** 2026-04-01
>
> This document outlines the phased development plan for the D'Jobsite iConnect backend — a modular monolith (C#/.NET 10) with a standalone AI Service microservice (Python/FastAPI).

---

## Current State Summary

### What's Built

- **Core architecture** — Modular monolith structure, middleware pipeline (CorrelationId, RequestLogging, AppError, TenantResolution), DI composition
- **SharedKernel** — Base classes (`Entity`, `AggregateRoot`), error handling (`AppError`/`AppErrors`), 7 domain events, result types
- **Module scaffolds** — All 8 modules have `Domain`, `Application`, `Infrastructure`, `Api` layer projects
- **Database designs** — All 9 schema design documents completed (Catalog + 8 per-tenant schemas)
- **Conventions & docs** — API, .NET, database, error envelope, testing, and contribution guides
- **AI Service scaffold** — FastAPI app with health endpoint, project structure, and dependencies defined
- **Test projects** — Unit, Integration, and Architecture test projects created (placeholder tests only)

### What Needs Implementation

- All module endpoints and business logic
- EF Core entity configurations and migrations
- Message broker integration (monolith ↔ AI Service) — deferred until AI Interview capability is built
- AI Service endpoints: resume parsing, criteria generation, assessment question generation, AI screening
- AI Interview Service endpoints, models, and media handling (deferred)
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

| #   | Task                              | Layer                   | Details                                                                                                                                                       |
| --- | --------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 3.1 | **Applicant profile entity**      | Domain / Infrastructure | `ApplicantProfile` (shared PK with `auth.users`), JSONB fields for `skills`, `social_links`, `documents`                                                      |
| 3.2 | **Resume entity**                 | Domain / Infrastructure | `Resume` entity with versioning support, `parsed_content` JSONB for pre-parsed data, `ai_parsed_content` JSONB for AI-powered structured extraction           |
| 3.3 | **EF Core migration**             | Infrastructure          | `profiles.applicant_profiles`, `profiles.resumes` with cross-schema FK to `auth.users`                                                                        |
| 3.4 | **Profile CRUD endpoints**        | Api                     | `GET/POST/PATCH /api/v1/profiles/me` — applicant self-service profile management                                                                              |
| 3.5 | **Resume upload endpoint**        | Api                     | `POST /api/v1/profiles/me/resumes` — file upload, storage, trigger parse job                                                                                  |
| 3.6 | **Resume parsing background job** | Infrastructure          | Background service to parse uploaded resumes — basic extraction (text, skills) always runs                                                                    |
| 3.7 | **AI Service HTTP client**        | Infrastructure          | HTTP client for `POST /api/v1/ai/resumes/parse` — AI-powered structured extraction (skills with levels/years, experience timeline, education, certifications) |
| 3.8 | **AI resume parsing integration** | Infrastructure          | Call AI Service during resume upload background job; store result in `ai_parsed_content`; graceful fallback to basic parser if AI Service unavailable         |
| 3.9 | **Tests**                         | Tests                   | Profile creation linked to auth user, resume versioning, parsing pipeline, AI parse fallback                                                                  |

**Exit Criteria:** Applicants have profiles linked 1:1 with auth users. Resumes are uploaded, versioned, and parsed asynchronously. AI-parsed structured data is available for downstream consumers. Basic parser output is used as fallback when AI is unavailable.

---

### Phase 4 — Recruitment Module

> Recruiters post jobs with evaluation criteria and screening questions; applicants submit applications.

| #    | Task                                | Layer                   | Details                                                                                                                                                                                                 |
| ---- | ----------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4.1  | **Client company entity**           | Domain / Infrastructure | `ClientCompany` for agency-model recruiting                                                                                                                                                             |
| 4.2  | **Job posting entity**              | Domain / Infrastructure | `JobPosting` aggregate — title, description, status lifecycle (Draft → Published → Closed)                                                                                                              |
| 4.3  | **Application entity**              | Domain / Infrastructure | `Application` aggregate — the pipeline spine; status enum tracking full lifecycle (including `Assessment`); one-app-per-person-per-job constraint                                                       |
| 4.4  | **Job evaluation criteria entity**  | Domain / Infrastructure | `JobEvaluationCriteria` — name, category (Skill/Experience/Certification/Education/Location/Custom), evaluation method (ExactMatch/RangeMatch/SemanticSimilarity), weight, configuration JSONB          |
| 4.5  | **Job screening questions entity**  | Domain / Infrastructure | `JobScreeningQuestion` — question_text, question_type (FreeText/MultipleChoice/YesNo), timing (AtApplication/AfterScreening), expected_answer JSONB, options JSONB                                      |
| 4.6  | **EF Core migration**               | Infrastructure          | `recruitment.client_companies`, `recruitment.job_postings`, `recruitment.applications`, `recruitment.job_evaluation_criteria`, `recruitment.job_screening_questions` with indexes and CHECK constraints |
| 4.7  | **Job posting CRUD**                | Api                     | `POST/GET/PATCH /api/v1/recruitment/job-postings` — create, list (paginated), update, publish/close                                                                                                     |
| 4.8  | **Criteria CRUD endpoints**         | Api                     | `POST/GET/PATCH/DELETE /api/v1/recruitment/job-postings/{id}/criteria` — manage evaluation criteria per job                                                                                             |
| 4.9  | **Questions CRUD endpoints**        | Api                     | `POST/GET/PATCH/DELETE /api/v1/recruitment/job-postings/{id}/questions` — manage screening questions per job                                                                                            |
| 4.10 | **AI-assisted criteria generation** | Api / Infrastructure    | Call AI Service `POST /api/v1/ai/criteria/suggest` — suggest criteria from job description; recruiter reviews and saves                                                                                 |
| 4.11 | **AI-assisted question generation** | Api / Infrastructure    | Call AI Service `POST /api/v1/ai/assessment/suggest` — suggest AfterScreening questions (feature-flagged: system gate + tenant `ai_assessment_questions_enabled`); recruiter reviews and saves          |
| 4.12 | **Application submission**          | Api                     | `POST /api/v1/recruitment/job-postings/{id}/applications` — validate one-per-person-per-job, attach resume reference, accept AtApplication question answers                                             |
| 4.13 | **Application listing**             | Api                     | `GET /api/v1/recruitment/applications` — filterable by status, job posting; cursor-based pagination                                                                                                     |
| 4.14 | **ApplicationSubmittedEvent**       | Domain                  | Publish `ApplicationSubmittedEvent` via MediatR when application is created                                                                                                                             |
| 4.15 | **Tests**                           | Tests                   | One-app-per-person-per-job enforcement, criteria CRUD, questions CRUD, AI suggestion integration, status transitions, event publishing                                                                  |

**Exit Criteria:** Jobs can be posted with evaluation criteria and screening questions. AI can suggest criteria and questions. Applicants can apply (one per job) and answer AtApplication questions. `ApplicationSubmittedEvent` fires for downstream modules.

---

### Phase 5 — Screening Module

> Criteria-driven screening with dual scoring engine, assessment flow, and candidate transparency.

| #    | Task                                  | Layer                   | Details                                                                                                                                                                                                                                                                                                                                 |
| ---- | ------------------------------------- | ----------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 5.1  | **Screening result entity**           | Domain / Infrastructure | `ScreeningResult` (shared PK with applications) — `overall_score`, `criteria_score_breakdown` (JSONB), `ai_criteria_score_breakdown` (JSONB, nullable), `ai_overall_score` (nullable), `question_score_breakdown` (JSONB, nullable), `assessment_score` (nullable), `candidate_feedback` (JSONB, nullable), `match_strength`, `outcome` |
| 5.2  | **Question response entity**          | Domain / Infrastructure | `ScreeningQuestionResponse` — application_id, question_id (cross-schema FK), response_text, response_data (JSONB), score, score_result, score_reasoning                                                                                                                                                                                 |
| 5.3  | **EF Core migration**                 | Infrastructure          | `screening.screening_results`, `screening.screening_question_responses` with cross-schema FKs and CHECK constraints                                                                                                                                                                                                                     |
| 5.4  | **Deterministic scoring service**     | Application             | Score each criterion using ExactMatch/RangeMatch rules and keyword overlap for SemanticSimilarity; build `criteria_score_breakdown`; compute `overall_score` using criterion weights                                                                                                                                                    |
| 5.5  | **AI scoring service**                | Application             | When enabled (system gate + tenant `ai_scoring_enabled`): call AI Service `POST /api/v1/ai/screening/evaluate`; build `ai_criteria_score_breakdown` and `ai_overall_score`. When disabled or unavailable: fields stay null                                                                                                              |
| 5.6  | **Question scoring service**          | Application             | Score AtApplication question answers: MultipleChoice/YesNo deterministically (always); FreeText via AI Service `POST /api/v1/ai/screening/score-answers` (always — independent of AI scoring flag)                                                                                                                                      |
| 5.7  | **Three-tier routing logic**          | Application             | Auto-advance based on deterministic `overall_score`: above threshold → Assessment (if AfterScreening questions exist) or Shortlisted (if none); below threshold → Rejected; between → `manual_review_policy`                                                                                                                            |
| 5.8  | **ApplicationSubmittedEvent handler** | Application             | Listen for `ApplicationSubmittedEvent` → run deterministic + AI scoring → score questions → route → publish `CvScreeningCompletedEvent`                                                                                                                                                                                                 |
| 5.9  | **Assessment flow**                   | Application             | Handle AfterScreening question submission → score answers → compute `assessment_score` → apply `completion_policy` (AutoAdvance / QueueForReview) → publish `AssessmentCompletedEvent`                                                                                                                                                  |
| 5.10 | **Candidate transparency**            | Application             | When enabled (tenant `candidate_transparency_enabled`): call AI Service `POST /api/v1/ai/screening/feedback` → store `candidate_feedback` on screening result                                                                                                                                                                           |
| 5.11 | **Re-scoring support**                | Application             | When job criteria are modified: mark affected screening results for re-evaluation; re-run scoring pipeline; update `criteria_score_breakdown` and `overall_score`                                                                                                                                                                       |
| 5.12 | **Screening results endpoint**        | Api                     | `GET /api/v1/screening/results` — view scores, criteria breakdowns, AI analysis (side-by-side when available); `PATCH` for manual review overrides                                                                                                                                                                                      |
| 5.13 | **Assessment endpoints**              | Api                     | `POST /api/v1/screening/applications/{id}/assessment` — submit AfterScreening question answers; `GET` — view assessment status                                                                                                                                                                                                          |
| 5.14 | **Candidate feedback endpoint**       | Api                     | `GET /api/v1/screening/applications/{id}/feedback` — candidate-facing evaluation summary (when transparency enabled)                                                                                                                                                                                                                    |
| 5.15 | **Tests**                             | Tests                   | Deterministic scoring algorithm, AI scoring integration, question scoring, threshold routing, assessment flow, re-scoring, candidate feedback, event chain                                                                                                                                                                              |

**Exit Criteria:** Applications are scored with dual engine (deterministic always, AI opt-in). Criteria-based scoring replaces fixed sub-scores. Three-tier routing works with Assessment or Shortlisted destinations. Assessment questions are scored and advance candidates. Candidate transparency is opt-in per tenant.

---

### Phase 6 — AI Service

> The standalone Python microservice centralizing all AI capabilities for the platform.

| #    | Task                                 | Layer                         | Details                                                                                                                                                                  |
| ---- | ------------------------------------ | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 6.1  | **SQLAlchemy models (core)**         | Infrastructure / DB           | `ai_api_logs`, `parsed_resume_cache` tables in `ai_service` schema                                                                                                       |
| 6.2  | **Alembic migration (core)**         | Migrations                    | Initial migration for `ai_service` schema — active tables only                                                                                                           |
| 6.3  | **AI provider abstraction**          | Infrastructure / AI Providers | OpenAI integration with provider abstraction layer; support for multiple providers                                                                                       |
| 6.4  | **Resume parsing endpoint**          | Api / Core                    | `POST /api/v1/ai/resumes/parse` — accept resume text, return structured extraction (skills with levels/years, experience, education, certifications); cache by file hash |
| 6.5  | **Criteria generation endpoint**     | Api / Core                    | `POST /api/v1/ai/criteria/suggest` — accept job description + title, return suggested evaluation criteria                                                                |
| 6.6  | **Assessment question endpoint**     | Api / Core                    | `POST /api/v1/ai/assessment/suggest` — accept job description + criteria, return suggested AfterScreening questions in `job_screening_questions` format                  |
| 6.7  | **AI screening evaluation endpoint** | Api / Core                    | `POST /api/v1/ai/screening/evaluate` — accept parsed resume + criteria, return per-criterion AI scores with reasoning                                                    |
| 6.8  | **Answer scoring endpoint**          | Api / Core                    | `POST /api/v1/ai/screening/score-answers` — accept question + answer + rubric, return score with reasoning                                                               |
| 6.9  | **Candidate feedback endpoint**      | Api / Core                    | `POST /api/v1/ai/screening/feedback` — accept criteria breakdown + scores, return candidate-facing feedback summary                                                      |
| 6.10 | **AI API logging**                   | Infrastructure                | Log all AI provider calls to `ai_api_logs` — token usage, cost estimation, latency, success/failure                                                                      |
| 6.11 | **JWT validation middleware**        | Core                          | Validate tokens issued by monolith Auth module                                                                                                                           |
| 6.12 | **Tenant ID extraction**             | Core                          | Extract tenant_id from JWT for cost attribution and logging                                                                                                              |
| 6.13 | **Tests**                            | Tests                         | pytest — resume parsing, criteria generation, screening evaluation, answer scoring, API endpoint tests, caching                                                          |

> **Note:** AI Interview endpoints (session management, question delivery, response submission, media transcription), broker consumer/publisher (`CandidateReadyForInterviewEvent` / `InterviewCompletedEvent`), and interview database tables are **deferred** to a future release. See TODO.md for the deferred items list.

**Exit Criteria:** AI Service provides resume parsing, criteria generation, assessment question suggestions, AI screening evaluation, answer scoring, and candidate feedback generation via HTTP endpoints. All API calls are logged for cost tracking. Resume parse results are cached by file hash.

---

### Phase 7 — Matching Module

> Combine screening and assessment scores to rank candidates and build shortlists.

| #   | Task                          | Layer                   | Details                                                                                                                                                            |
| --- | ----------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 7.1 | **Candidate match entity**    | Domain / Infrastructure | `CandidateMatch` — composite score from screening + assessment, rank                                                                                               |
| 7.2 | **Shortlist entities**        | Domain / Infrastructure | `Shortlist` aggregate, `ShortlistCandidate` — per-job-posting shortlists                                                                                           |
| 7.3 | **EF Core migration**         | Infrastructure          | `matching.candidate_matches`, `matching.shortlists`, `matching.shortlist_candidates`                                                                               |
| 7.4 | **Score aggregation service** | Application             | Consume `CvScreeningCompletedEvent` + `AssessmentCompletedEvent` → compute composite score using `screening_weight` + `assessment_weight` from `matching_settings` |
| 7.5 | **Shortlist generation**      | Application             | Rank candidates by composite score, generate shortlists for hiring manager review                                                                                  |
| 7.6 | **CandidateShortlistedEvent** | Domain                  | Publish when candidate is added to shortlist                                                                                                                       |
| 7.7 | **Shortlist endpoints**       | Api                     | `GET /api/v1/matching/shortlists` — view/manage shortlists; approve/reject candidates                                                                              |
| 7.8 | **Tests**                     | Tests                   | Score aggregation with screening + assessment weights, ranking, shortlist generation                                                                               |

**Exit Criteria:** Screening and assessment scores are combined using tenant-configurable weights. Candidates are ranked and shortlisted. Hiring managers can review and approve shortlists.

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
    ├── Phase 2: Admin ──────────────────┐        │   │
    │                                    │        │   │
    ├── Phase 6: AI Service ─────────────┤  (core built early, endpoints added incrementally)
    │       │                            │        │   │
    │   Phase 3: Profiles ───────────────┤  (needs AI Service for resume parsing)
    │       │                            │        │   │
    │   Phase 4: Recruitment ────────────┤  (needs AI Service for criteria + question suggestions)
    │       │                            │        │   │
    │   Phase 5: Screening ──────────────┤  (needs AI Service for AI scoring + answer scoring)
    │                                    │        │   │
    │   Phase 7: Matching ───────────────┤  (consumes Screening + Assessment scores)
    │       │                            │        │   │
    │   Phase 8: HR Workflows ───────────┘  (consumes Matching shortlists)
    │
Phase 9: Hardening ──────────────────────────────────┘
```

**Parallelization opportunities:**

- Phases 2 (Admin) and 6 (AI Service core) can be developed in parallel after Phase 1
- AI Service endpoints are built incrementally: resume parsing (6.4) before Phase 3, criteria/assessment (6.5–6.6) before Phase 4, screening endpoints (6.7–6.9) before Phase 5
- Phases 3 (Profiles) and 4 (Recruitment) can overlap once AI Service core is ready

---

## Risk Register

| Risk                                  | Impact                                                                                         | Mitigation                                                                                                                                                               |
| ------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| AI provider API changes/outages       | AI Service capabilities degraded — resume parsing, criteria, screening, questions all affected | Provider abstraction layer; fallback to secondary provider; circuit breaker; deterministic scoring always available as fallback                                          |
| AI scoring consistency                | Different AI runs produce different scores for same input                                      | Deterministic scoring drives automation; AI analysis is advisory only; scores stored for auditability                                                                    |
| AI Service cost scaling               | High AI API costs as tenant/application volume grows                                           | Two-layer feature flags (system gate + tenant opt-in); resume parse caching by file hash; per-tenant cost tracking via `ai_api_logs`; deterministic scoring is zero-cost |
| Multi-tenant DB provisioning at scale | Slow tenant onboarding                                                                         | Connection pooling; async provisioning pipeline; provisioning queue                                                                                                      |
| Cross-module event ordering           | Data inconsistency                                                                             | Idempotent event handlers; outbox pattern for reliable publishing                                                                                                        |
| Resume parsing accuracy               | Poor screening results                                                                         | Dual parsing (basic always + AI when enabled); AI-parsed structured data validated; graceful fallback                                                                    |
| Token replay attacks                  | Auth compromise                                                                                | Refresh token families; rotation on every use; replay detection revokes family                                                                                           |
| Tenant data leakage                   | Security/compliance breach                                                                     | Database-per-tenant isolation; middleware-level enforcement; integration tests per tenant boundary                                                                       |

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
