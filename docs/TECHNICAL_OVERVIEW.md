# Technical Overview — D'Jobsite iConnect

This document covers the platform's architecture, module responsibilities, data flow between components, and the key design decisions behind the system.

For database-level details (table schemas, indexes, constraints, JSONB formats), see the individual `*_DB_DESIGN.md` documents linked from the README.

---

## Architecture

D'Jobsite iConnect is built as a **modular monolith** with one **standalone microservice**.

The monolith contains eight modules that share a runtime process and a per-tenant database, but are isolated at the code level — each module owns its own PostgreSQL schema, entities, and business logic. Modules communicate through in-process domain events via MediatR.

The AI Service is deployed separately because it has fundamentally different scaling and lifecycle needs: compute-intensive AI provider calls, specialized AI model integrations, and (eventually) media transcription and async batch processing. It communicates with the monolith via **HTTP for synchronous operations** (resume parsing, criteria generation, assessment question generation, AI screening) and via **message broker for asynchronous operations** (future AI Interview).

```
                        ┌─────────────────────────┐
                        │      Catalog DB          │
                        │  (shared, one instance)  │
                        │                          │
                        │  Tenant metadata,        │
                        │  subdomains, branding,   │
                        │  subscriptions           │
                        └────────────┬─────────────┘
                                     │
                                     │ tenant resolution
                                     │ (cached in Redis)
                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        Modular Monolith                                │
│                                                                        │
│  Request → TenantResolutionMiddleware → JWT Auth → Module Controllers  │
│                                                                        │
│  ┌──────────┐  ┌──────┐  ┌───────────┐  ┌───────────┐  ┌──────────┐  │
│  │ Tenancy  │  │ Auth │  │Recruitment│  │ Screening │  │ Matching │  │
│  │          │  │      │  │           │  │           │  │          │  │
│  │catalog.* │  │auth.*│  │recruit.*  │  │screening.*│  │matching.*│  │
│  └──────────┘  └──────┘  └───────────┘  └───────────┘  └──────────┘  │
│  ┌──────────────┐  ┌──────────┐  ┌───────┐                           │
│  │ HR Workflows │  │ Profiles │  │ Admin │                           │
│  │              │  │          │  │       │                           │
│  │hr_workflows.*│  │profiles.*│  │admin.*│                           │
│  └──────────────┘  └──────────┘  └───────┘                           │
│                                                                        │
│  Internal communication: MediatR domain events                         │
└──────────────────────────┬─────────────────┬───────────────────────────┘
                           │                 │
           HTTP (sync)     │                 │  Integration events
           ┌──────────────┘                 │  (RabbitMQ / Azure Service Bus)
           │                                 │  (future — AI Interview)
           ▼                                 ▼
┌──────────────────────────────────┐     ┌──────────────────────┐
│         AI Service               │────▶│     AI Service DB     │
│    (separate deployment)         │     │   (shared, one       │
│                                  │     │    instance, tenant   │
│  Resume parsing, criteria gen,   │     │    ID filtering)     │
│  AI screening, assessment Qs,    │     └──────────────────────┘
│  AI interview (future)           │
└──────────────────────────────────┘
```

---

## Multi-Tenancy Strategy

The platform uses **database-per-tenant** for the monolith and **shared database with tenant ID filtering** for the AI Service.

### Monolith: Database Per Tenant

Every tenant gets their own PostgreSQL database. All monolith modules read from and write to the same per-tenant database — there is not a separate database per module. Module isolation is enforced at the code and schema level, not the database level.

The **Catalog DB** is the one shared database. It stores only tenant metadata: name, subdomain, connection string, subscription tier, and branding. No user data lives here.

**Request flow:**

1. Request arrives at `acme.djobsite.com`
2. `TenantResolutionMiddleware` extracts the subdomain
3. Catalog DB lookup (Redis-cached) returns the tenant's connection string and branding
4. `TenantDbContext` is configured to point at the tenant's database for the rest of the request
5. JWT validation and all downstream queries run against the tenant's database

**Why database-per-tenant?** Full data isolation without tenant ID filters on every query. No risk of cross-tenant data leakage from a missing WHERE clause. Simpler per-tenant backup, restore, and compliance (e.g., data deletion for GDPR). The operational overhead is manageable because tenants are businesses (hundreds to low thousands), not individual users.

### AI Service: Shared Database

The AI Service uses a single database for all tenants with `tenant_id` on every table and as the leading column in composite indexes.

**Why not database-per-tenant here?** The AI Service stores lightweight, mostly-transient data: API call logs for cost tracking and cached parse results for deduplication. Provisioning and managing a separate database per tenant for this data adds operational complexity for no meaningful benefit. A single database with tenant filtering is simpler and sufficient at the expected scale.

---

## Module Responsibilities

Each module follows a consistent internal structure: Domain (entities), Application (services, DTOs, events), Infrastructure (persistence, integrations), and Api (controllers).

### Tenancy

The only module that touches the Catalog DB. Handles tenant registration, subdomain validation, database provisioning (creating a new PostgreSQL database and running migrations), seeding the initial admin user, connection string management, and subscription tier enforcement. Called by the middleware on every request for tenant resolution.

### Auth

Custom authentication — not ASP.NET Identity. Manages user registration (self-registration for applicants, admin invite for staff), email/password login, OAuth login (Google, Apple, Facebook), JWT access tokens with tenant/user/role claims, and refresh token rotation with replay detection. All auth data lives in the tenant database. The JWT secret is shared with the AI Service so both can validate tokens independently.

### Admin

Tenant-scoped control plane. Owns two things: a singleton `company_settings` row (JSONB columns for auth config, profile requirements, screening thresholds, matching weights, assessment settings, and notification preferences) and an append-only `audit_logs` table. Also provides dashboard aggregation by reading from other modules' tables via read-only queries. A separate `PlatformAdminController` handles system-wide operations against the Catalog DB.

### Profiles

Applicant professional identity — summary, skills, contact details, social links, documents, and resume management. Resumes are parsed once on upload by a background job (basic text extraction + skill extraction, plus AI-powered structured extraction when enabled). Basic parsing always runs; AI parsing calls the AI Service for richer extraction (skills with levels/years, experience timeline, education details, certifications) and stores the result in `ai_parsed_content`. If the AI Service is unavailable, the basic parser output is used as fallback. Downstream modules read pre-parsed data — no re-downloading or re-parsing. This is a passive data module with no outbound events.

### Recruitment

Core of the hiring pipeline. Manages client companies (for agency tenants), job postings (draft → published → closed), evaluation criteria, screening questions, and application intake. Each job posting has configurable evaluation criteria (`job_evaluation_criteria` — Skill, Experience, Certification, Education, Location, Custom categories with ExactMatch/RangeMatch/SemanticSimilarity methods) and optional screening questions (`job_screening_questions` — FreeText, MultipleChoice, YesNo types with AtApplication or AfterScreening timing). AI-assisted criteria and question suggestions are available via the AI Service (assessment questions are feature-flagged). The application record is the spine of the entire pipeline — every downstream module references it. Publishes `ApplicationSubmittedEvent` when a new application comes in, which kicks off the automated pipeline. Enforces one application per person per job.

### Screening

Criteria-driven screening and evaluation engine. Receives `ApplicationSubmittedEvent`, then scores the application against `job_evaluation_criteria` using pre-parsed resume data (including AI-parsed structured data when available) and the applicant's profile. Runs a **dual scoring engine**: deterministic scoring always runs (ExactMatch/RangeMatch rules, keyword overlap for SemanticSimilarity), producing `criteria_score_breakdown` and `overall_score`. When AI scoring is enabled (system-wide gate + tenant opt-in), the AI Service provides parallel AI analysis scores (`ai_criteria_score_breakdown`, `ai_overall_score`) with richer reasoning — advisory for recruiter decisions, not used for automation. AtApplication question answers are also scored (MultipleChoice/YesNo deterministically, FreeText always via AI Service). Routes through a three-tier model based on deterministic score: auto-advance to Assessment (if AfterScreening questions exist) or Shortlisted (if none), auto-reject, or manual review. Publishes `CvScreeningCompletedEvent`. Also handles the Assessment flow: AfterScreening question answers are scored, `assessment_score` computed, and `AssessmentCompletedEvent` published. Candidate transparency — generating evaluation feedback for candidates — is opt-in per tenant.

### Matching

Ranks and shortlists candidates after screening and assessment scores are available. Combines screening scores and assessment scores using tenant-configurable weights (`screening_weight` + `assessment_weight`). Generates shortlists for hiring managers. Listens for `CvScreeningCompletedEvent` (from Screening) and `AssessmentCompletedEvent` (from Screening, after assessment answers are scored). Publishes `CandidateShortlistedEvent` to HR Workflows.

### HR Workflows

The human side of hiring. Schedules final interviews with panel support (multiple interviewers providing independent feedback), aggregates panelist recommendations into an overall hire/no-hire decision by the hiring manager, and manages job offers through a draft → pending → accepted/declined/expired/withdrawn lifecycle. Listens for `CandidateShortlistedEvent`. Publishes `FinalInterviewScheduledEvent` and `OfferExtendedEvent`.

### AI Service (Microservice)

Standalone deployment with its own database. Centralizes all AI capabilities for the platform. Currently provides four active capabilities via HTTP: (1) **resume parsing** — AI-powered structured data extraction from resumes, (2) **criteria generation** — AI-suggested evaluation criteria from job descriptions, (3) **assessment question generation** — AI-suggested screening questions (feature-flagged), (4) **AI screening** — alternative scoring engine providing per-criterion AI analysis (feature-flagged). A fifth capability — **AI interview** (real-time AI-conducted interview sessions with media handling, transcription, and scoring) — is designed but deferred to a future release. The monolith communicates with the AI Service via direct HTTP calls for synchronous operations. A message broker connection is reserved for the future AI Interview capability (async, long-running sessions). Logs every AI API call for cost tracking, debugging, and compliance.

---

## Event Flow

The hiring pipeline is orchestrated through two types of events:

**Domain events** (MediatR, in-process, synchronous within the monolith):

| Event                          | Publisher    | Consumer        |
| ------------------------------ | ------------ | --------------- |
| `ApplicationSubmittedEvent`    | Recruitment  | Screening       |
| `CvScreeningCompletedEvent`    | Screening    | Matching        |
| `AssessmentCompletedEvent`     | Screening    | Matching        |
| `CandidateShortlistedEvent`    | Matching     | HR Workflows    |
| `FinalInterviewScheduledEvent` | HR Workflows | (notifications) |
| `OfferExtendedEvent`           | HR Workflows | (notifications) |

**Future integration events** (message broker, async, cross-service — deferred until AI Interview is implemented):

| Event                             | Publisher  | Consumer   |
| --------------------------------- | ---------- | ---------- |
| `CandidateReadyForInterviewEvent` | Screening  | AI Service |
| `InterviewCompletedEvent`         | AI Service | Matching   |

Domain events also trigger audit log entries in the Admin module.

---

## Application Lifecycle

An application moves through these statuses, driven by events from the modules that own each pipeline stage:

```
Submitted ──→ Screening ──→ [Assessment] ──→ Shortlisted ──→ FinalInterview ──→ Offered ──→ Hired
                 │               │                  │                │              │
                 ▼               ▼                  ▼                ▼              ▼
              Rejected       Rejected           Rejected         Rejected       Rejected

Any stage ──→ Withdrawn (applicant pulls out voluntarily)
```

The `[Assessment]` stage is optional — it is only entered when a job has `AfterScreening` questions configured. Jobs with only `AtApplication` questions (or no questions) skip directly from Screening to Shortlisted.

The Recruitment module owns the application status field, but transitions are triggered by downstream modules through domain events — Recruitment doesn't decide when to move to "Screening" or "Shortlisted."

---

## Screening Pipeline Detail

Screening is the most configurable part of the automated pipeline. Each tenant controls:

- **Evaluation criteria** — Per-job criteria with category, evaluation method, weight, and required/optional status. Defaults from tenant `screening_settings.default_evaluation_criteria`, customized by recruiter per job.
- **Dual scoring engine** — Deterministic scoring always runs (zero-cost, rules-based). AI analysis scoring is opt-in (system gate + tenant `ai_scoring_enabled`), runs alongside deterministic, and provides richer reasoning for recruiter review.
- **Auto-advance threshold** — Minimum deterministic score to skip human review (default: 70). Advances to Assessment (if AfterScreening questions exist) or Shortlisted.
- **Auto-reject threshold** — Maximum deterministic score to automatically reject (default: 30).
- **Manual review policy** — What happens in the gray zone between thresholds: queue for recruiter review, auto-advance all, auto-reject all, or notify and hold.
- **Candidate transparency** — Opt-in per tenant. Generates candidate-facing feedback (summary or detailed per-criteria breakdown).

```
Score: 100 ────────────────
                            → Auto-advance to Assessment / Shortlisted
Score:  70 ──────────────── (configurable threshold)
                            → Tenant's manual review policy applies
Score:  30 ──────────────── (configurable threshold)
                            → Auto-reject
Score:   0 ────────────────
```

The thresholds active at evaluation time are captured on each screening result so historical decisions remain interpretable even if the tenant changes their configuration later.

### Assessment Flow

The Assessment stage is optional. It is entered only when a job has `AfterScreening` questions configured.

1. Candidate reaches Assessment stage (auto-advanced or manually advanced after screening)
2. AfterScreening questions are presented to the candidate
3. Candidate submits answers → `screening_question_responses` rows created
4. Answers are scored: MultipleChoice/YesNo deterministically, FreeText via AI Service
5. `assessment_score` computed from question score breakdown
6. Final pipeline score = weighted combo of screening `overall_score` + `assessment_score` (weights from `matching_settings`: `screening_weight` + `assessment_weight`)
7. Tenant's `completion_policy` applied: `AutoAdvance` → advance to Shortlisted; `QueueForReview` → recruiter reviews before advancing
8. `AssessmentCompletedEvent` published

---

## AI Interview Pipeline Detail (Deferred)

> **⚠️ DEFERRED** — The AI Interview capability is designed but not yet implemented. It will be built as part of the AI Service when prioritized. The Assessment stage (recruiter-defined screening questions) handles the post-screening evaluation use case in the current design.

When implemented, the AI Interview Service will process each interview in three phases:

**Phase 1 — Setup (async, before candidate starts):**
The service receives `CandidateReadyForInterviewEvent` via the message broker, creates a session, calls the AI provider to generate questions based on job requirements (respecting the tenant's configured question mix and count), and marks the session as ready.

**Phase 2 — Candidate answering (real-time):**
The candidate answers questions via the API. No scoring happens during this phase. Responses can be text, voice, or video.

**Phase 3 — Processing (async, candidate is done):**
Voice/video responses are transcribed. All responses are scored individually by the AI. A comprehensive evaluation is generated with per-category scores, an overall weighted score, strengths, concerns, and a recommendation. `InterviewCompletedEvent` is published to the broker.

See [AI Service DB Design](database-designs/AI_INTERVIEW_DB_DESIGN.md) for the deferred table schemas.

---

## Cross-Cutting Concerns

### Caching

Tenant resolution is the hottest path — every request needs it. The full tenant object (including branding) is cached in Redis, keyed by subdomain. Cache invalidation happens only on tenant metadata or branding updates, which are rare.

### Audit Logging

The Admin module's `audit_logs` table is append-only and FK-free. Actor identity (ID, email, role) is denormalized at write time so entries survive user deletion or role changes. Coverage includes user management, job posting lifecycle, screening decisions, interview outcomes, offer actions, and settings changes.

### Resume Parsing

Resumes are parsed once on upload by a background job in the Profiles module. Basic text extraction and skill extraction always run. When AI parsing is enabled (`ai_parsing_enabled` in `profile_settings`), the Profiles module calls the AI Service for richer structured extraction — skills with levels and years of experience, experience timeline, education details, and certifications — stored as `ai_parsed_content` on the resume record. If the AI Service is unavailable, the basic parser output is used as fallback. Every downstream consumer (Screening, Matching) reads pre-parsed data — no re-downloading, no re-parsing, no duplication across applications.

### Configuration

Tenant-specific behavior is controlled by the `company_settings` singleton in each tenant database. Settings are grouped into JSONB columns by domain: auth, profiles, screening, matching, assessment, and notifications. Defaults are seeded during provisioning. Changes are made through the admin portal and audited.

---

## Database Schema Ownership

Each module owns its tables under a dedicated PostgreSQL schema within the tenant database:

| Schema           | Module       | Key Tables                                                                                                                                                                  |
| ---------------- | ------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `catalog.*`      | Tenancy      | `tenants`, `tenant_brandings`                                                                                                                                               |
| `auth.*`         | Auth         | `users`, `user_external_logins`, `refresh_tokens`                                                                                                                           |
| `admin.*`        | Admin        | `company_settings`, `audit_logs`                                                                                                                                            |
| `profiles.*`     | Profiles     | `applicant_profiles`, `resumes`                                                                                                                                             |
| `recruitment.*`  | Recruitment  | `client_companies`, `job_postings`, `applications`, `job_evaluation_criteria`, `job_screening_questions`                                                                    |
| `screening.*`    | Screening    | `screening_results`, `screening_question_responses`                                                                                                                         |
| `matching.*`     | Matching     | `candidate_matches`, `shortlists`, `shortlist_candidates`                                                                                                                   |
| `hr_workflows.*` | HR Workflows | `final_interviews`, `interview_panelists`, `job_offers`                                                                                                                     |
| `ai_service.*`   | AI Service   | Active: `ai_api_logs`, `parsed_resume_cache`. Deferred: `interview_sessions`, `interview_questions`, `interview_responses`, `response_evaluations`, `interview_evaluations` |

Cross-schema foreign keys exist where referential integrity matters more than strict module isolation — particularly for `auth.users` (identity is foundational) and `recruitment.applications` (the pipeline spine).

If Module A needs data from Module B, it goes through a shared interface or listens for a domain event — not by querying Module B's tables directly. The schema boundary is a visible reminder of that rule.

---

## Key Design Decisions

**Modular monolith over microservices.** The core HR modules share a database, a deployment, and a runtime. This is simpler to develop, deploy, debug, and reason about than eight separate services with their own databases and network boundaries. The AI Service is the one exception — its compute profile (AI provider API calls, model integration) and potential scaling needs are different enough to justify separation. It handles more than just interviews: resume parsing, criteria generation, and screening analysis are all centralized here.

**Database-per-tenant over tenant ID filtering (for the monolith).** Full isolation at the database level eliminates an entire class of bugs (missing WHERE clauses leaking data). Per-tenant backup and compliance operations are straightforward. The scale (hundreds to low thousands of tenants, not millions) makes this operationally feasible.

**Custom auth over ASP.NET Identity.** Identity Framework's rigid table schema and UserManager abstractions fight against database-per-tenant multi-tenancy. Custom auth gives full control over schema, token flow, and tenant resolution integration.

**Domain events over direct module calls.** Modules publish events when something happens; interested modules react. This keeps the dependency graph clean — Recruitment doesn't know about Screening's internals, it just publishes "an application was submitted" and moves on.

**JSONB for flexible structured data.** Skills, social links, documents, settings, criteria configurations, score breakdowns, and candidate feedback are stored as JSONB. This avoids schema migrations for data that grows unpredictably (new criteria categories, new score dimensions) while still being queryable via GIN indexes where needed.

**Dual scoring engine.** Deterministic scoring (rules-based, zero-cost) always runs and drives automation. AI analysis scoring is opt-in via two-layer feature flags (system gate + tenant opt-in) and provides advisory scores for recruiter review. This ensures the pipeline works without any AI dependency while allowing tenants to layer in AI analysis when ready.

**AI Service over embedded AI.** Centralizing AI capabilities in a separate service — rather than embedding AI provider calls in each monolith module — provides a single point for provider abstraction, cost tracking, caching, and model management. Multiple modules (Profiles, Recruitment, Screening) all consume AI capabilities through a consistent HTTP interface.

**Shared primary keys for one-to-one relationships.** Profiles, screening results, final interviews, job offers, and evaluations use shared primary keys with their parent table. This enforces one-to-one at the database level and avoids redundant ID columns.

**String enums over integer enums.** Stored as readable strings in the database, enforced by CHECK constraints. The small storage cost is irrelevant; the readability and query-friendliness are significant.

**Append-only audit logs with denormalized actor data.** Audit entries must survive user deletion, email changes, and role changes. Denormalizing the actor's identity at write time — with no foreign keys — ensures the audit trail is permanent and self-contained.

---

## Further Reading

Each module's database design is documented in detail:

- [Catalog DB](CATALOG_DB_DESIGN.md) — Tenant metadata, branding, provisioning
- [Auth DB](AUTH_DB_DESIGN.md) — Users, OAuth, refresh tokens, replay detection
- [Admin DB](ADMIN_DB_DESIGN.md) — Settings (all JSONB formats), audit logging
- [Profiles DB](PROFILES_DB_DESIGN.md) — Profiles, resume parsing, skills format
- [Recruitment DB](RECRUITMENT_DB_DESIGN.md) — Jobs, applications, client companies, evaluation criteria, screening questions
- [Screening DB](SCREENING_DB_DESIGN.md) — Criteria-based scoring, dual engine, assessment flow, candidate transparency
- [HR Workflows DB](HR_WORKFLOWS_DB_DESIGN.md) — Interviews, panels, offers
- [AI Service DB](AI_INTERVIEW_DB_DESIGN.md) — API logs, resume cache, deferred interview tables
- [CHECK Constraints](CHECK_CONSTRAINTS.md) — All enum constraints across the platform
