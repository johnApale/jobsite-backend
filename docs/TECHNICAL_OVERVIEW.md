# Technical Overview — D'Jobsite iConnect

This document covers the platform's architecture, module responsibilities, data flow between components, and the key design decisions behind the system.

For database-level details (table schemas, indexes, constraints, JSONB formats), see the individual `*_DB_DESIGN.md` documents linked from the README.

---

## Architecture

D'Jobsite iConnect is built as a **modular monolith** with one **standalone microservice**.

The monolith contains eight modules that share a runtime process and a per-tenant database, but are isolated at the code level — each module owns its own PostgreSQL schema, entities, and business logic. Modules communicate through in-process domain events via MediatR.

The AI Interview Service is deployed separately because it has fundamentally different scaling and lifecycle needs: compute-intensive AI provider calls, media transcription, and async batch processing. It communicates with the monolith exclusively through a message broker (RabbitMQ or Azure Service Bus).

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
└──────────────────────────┬─────────────────────────────────────────────┘
                           │
                           │ Integration events
                           │ (RabbitMQ / Azure Service Bus)
                           │
              ┌────────────▼───────────────┐     ┌──────────────────────┐
              │   AI Interview Service     │────▶│   AI Interview DB    │
              │   (separate deployment)    │     │   (shared, one       │
              │                            │     │    instance, tenant   │
              │   Question generation,     │     │    ID filtering)     │
              │   response scoring,        │     └──────────────────────┘
              │   evaluation, transcription│
              └────────────────────────────┘
```

---

## Multi-Tenancy Strategy

The platform uses **database-per-tenant** for the monolith and **shared database with tenant ID filtering** for the AI Interview Service.

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

### AI Interview Service: Shared Database

The AI Interview Service uses a single database for all tenants with `tenant_id` on every table and as the leading column in composite indexes.

**Why not database-per-tenant here?** Interview data is transient and compute-heavy. Sessions are created, scored, results sent back to the monolith, and eventually archived. The operational overhead of managing a separate database per tenant for short-lived data isn't justified.

---

## Module Responsibilities

Each module follows a consistent internal structure: Domain (entities), Application (services, DTOs, events), Infrastructure (persistence, integrations), and Api (controllers).

### Tenancy

The only module that touches the Catalog DB. Handles tenant registration, subdomain validation, database provisioning (creating a new PostgreSQL database and running migrations), seeding the initial admin user, connection string management, and subscription tier enforcement. Called by the middleware on every request for tenant resolution.

### Auth

Custom authentication — not ASP.NET Identity. Manages user registration (self-registration for applicants, admin invite for staff), email/password login, OAuth login (Google, Apple, Facebook), JWT access tokens with tenant/user/role claims, and refresh token rotation with replay detection. All auth data lives in the tenant database. The JWT secret is shared with the AI Interview Service so both can validate tokens independently.

### Admin

Tenant-scoped control plane. Owns two things: a singleton `company_settings` row (JSONB columns for auth config, profile requirements, screening thresholds, matching weights, AI interview settings, and notification preferences) and an append-only `audit_logs` table. Also provides dashboard aggregation by reading from other modules' tables via read-only queries. A separate `PlatformAdminController` handles system-wide operations against the Catalog DB.

### Profiles

Applicant professional identity — summary, skills, contact details, social links, documents, and resume management. Resumes are parsed once on upload by a background job (text extraction + skill extraction), and the results are stored on the resume record. Downstream modules read pre-parsed data — no re-downloading or re-parsing. This is a passive data module with no outbound events.

### Recruitment

Core of the hiring pipeline. Manages client companies (for agency tenants), job postings (draft → published → closed), and application intake. The application record is the spine of the entire pipeline — every downstream module references it. Publishes `ApplicationSubmittedEvent` when a new application comes in, which kicks off the automated pipeline. Enforces one application per person per job.

### Screening

Automated first-pass evaluation. Receives `ApplicationSubmittedEvent`, then scores the application using pre-parsed resume data and the applicant's profile skills against job requirements. Produces three sub-scores (skill match, experience match, resume quality) weighted by tenant configuration into an overall score. Routes the result through a three-tier model: auto-advance (above threshold), auto-reject (below threshold), or manual review (between thresholds, behavior governed by tenant policy). Publishes `CvScreeningCompletedEvent` and, for advancing candidates, `CandidateReadyForInterviewEvent` as an integration event to the AI Interview Service.

### Matching

Ranks and shortlists candidates after screening and AI interview scores are available. Combines screening scores and interview scores using tenant-configurable weights. Generates shortlists for hiring managers. Listens for `CvScreeningCompletedEvent` (from Screening) and `InterviewCompletedEvent` (from AI Interview Service via broker). Publishes `CandidateShortlistedEvent` to HR Workflows.

### HR Workflows

The human side of hiring. Schedules final interviews with panel support (multiple interviewers providing independent feedback), aggregates panelist recommendations into an overall hire/no-hire decision by the hiring manager, and manages job offers through a draft → pending → accepted/declined/expired/withdrawn lifecycle. Listens for `CandidateShortlistedEvent`. Publishes `FinalInterviewScheduledEvent` and `OfferExtendedEvent`.

### AI Interview Service (Microservice)

Standalone deployment with its own database. Receives `CandidateReadyForInterviewEvent` from the message broker, generates interview questions tailored to job requirements using an AI provider, presents them to candidates (text, voice, or video responses supported), transcribes media responses, scores each response individually, produces a comprehensive evaluation with per-category breakdowns, and publishes `InterviewCompletedEvent` back to the broker for the Matching module. Logs every AI API call for cost tracking, debugging, and compliance.

---

## Event Flow

The hiring pipeline is orchestrated through two types of events:

**Domain events** (MediatR, in-process, synchronous within the monolith):

| Event | Publisher | Consumer |
|-------|-----------|----------|
| `ApplicationSubmittedEvent` | Recruitment | Screening |
| `CvScreeningCompletedEvent` | Screening | Matching |
| `CandidateShortlistedEvent` | Matching | HR Workflows |
| `FinalInterviewScheduledEvent` | HR Workflows | (notifications) |
| `OfferExtendedEvent` | HR Workflows | (notifications) |

**Integration events** (message broker, async, cross-service):

| Event | Publisher | Consumer |
|-------|-----------|----------|
| `CandidateReadyForInterviewEvent` | Screening | AI Interview Service |
| `InterviewCompletedEvent` | AI Interview Service | Matching |

Domain events also trigger audit log entries in the Admin module.

---

## Application Lifecycle

An application moves through these statuses, driven by events from the modules that own each pipeline stage:

```
Submitted ──→ Screening ──→ AiInterview ──→ Shortlisted ──→ FinalInterview ──→ Offered ──→ Hired
                 │               │               │                │              │
                 ▼               ▼               ▼                ▼              ▼
              Rejected       Rejected        Rejected         Rejected       Rejected

Any stage ──→ Withdrawn (applicant pulls out voluntarily)
```

The Recruitment module owns the application status field, but transitions are triggered by downstream modules through domain events — Recruitment doesn't decide when to move to "Screening" or "Shortlisted."

---

## Screening Pipeline Detail

Screening is the most configurable part of the automated pipeline. Each tenant controls:

- **Score weights** — How much skill match, experience match, and resume quality contribute to the overall score (must sum to 100).
- **Auto-advance threshold** — Minimum score to skip human review and go straight to AI Interview (default: 70).
- **Auto-reject threshold** — Maximum score to automatically reject (default: 30).
- **Manual review policy** — What happens in the gray zone between thresholds: queue for recruiter review, auto-advance all, auto-reject all, or notify and hold.

```
Score: 100 ────────────────
                            → Auto-advance to AI Interview
Score:  70 ──────────────── (configurable threshold)
                            → Tenant's manual review policy applies
Score:  30 ──────────────── (configurable threshold)
                            → Auto-reject
Score:   0 ────────────────
```

The thresholds active at evaluation time are captured on each screening result so historical decisions remain interpretable even if the tenant changes their configuration later.

---

## AI Interview Pipeline Detail

The AI Interview Service processes each interview in three phases:

**Phase 1 — Setup (async, before candidate starts):**
The service receives the event, creates a session, calls the AI provider to generate questions based on job requirements (respecting the tenant's configured question mix and count), and marks the session as ready.

**Phase 2 — Candidate answering (real-time):**
The candidate answers questions via the API. No scoring happens during this phase — the candidate's experience is uninterrupted. Responses can be text, voice, or video.

**Phase 3 — Processing (async, candidate is done):**
Voice/video responses are transcribed (speech-to-text). All responses are scored individually by the AI. A comprehensive evaluation is generated with per-category scores (technical, communication, experience, behavioral), an overall weighted score, strengths, concerns, and a recommendation. The `InterviewCompletedEvent` is published to the broker.

**Why batch scoring after completion?** No waiting between questions for the candidate, transcription and scoring can be parallelized, the AI gets full context for the evaluation, and failures don't block the candidate mid-interview.

---

## Cross-Cutting Concerns

### Caching

Tenant resolution is the hottest path — every request needs it. The full tenant object (including branding) is cached in Redis, keyed by subdomain. Cache invalidation happens only on tenant metadata or branding updates, which are rare.

### Audit Logging

The Admin module's `audit_logs` table is append-only and FK-free. Actor identity (ID, email, role) is denormalized at write time so entries survive user deletion or role changes. Coverage includes user management, job posting lifecycle, screening decisions, interview outcomes, offer actions, and settings changes.

### Resume Parsing

Resumes are parsed once on upload by a background job in the Profiles module. The parsed text and extracted skills are stored on the resume record. Every downstream consumer (Screening, Matching) reads pre-parsed data — no re-downloading, no re-parsing, no duplication across applications.

### Configuration

Tenant-specific behavior is controlled by the `company_settings` singleton in each tenant database. Settings are grouped into JSONB columns by domain: auth, profiles, screening, matching, AI interview, and notifications. Defaults are seeded during provisioning. Changes are made through the admin portal and audited.

---

## Database Schema Ownership

Each module owns its tables under a dedicated PostgreSQL schema within the tenant database:

| Schema | Module | Key Tables |
|--------|--------|------------|
| `catalog.*` | Tenancy | `tenants`, `tenant_brandings` |
| `auth.*` | Auth | `users`, `user_external_logins`, `refresh_tokens` |
| `admin.*` | Admin | `company_settings`, `audit_logs` |
| `profiles.*` | Profiles | `applicant_profiles`, `resumes` |
| `recruitment.*` | Recruitment | `client_companies`, `job_postings`, `applications` |
| `screening.*` | Screening | `screening_results` |
| `matching.*` | Matching | `candidate_matches`, `shortlists`, `shortlist_candidates` |
| `hr_workflows.*` | HR Workflows | `final_interviews`, `interview_panelists`, `job_offers` |
| `ai_interview.*` | AI Interview Service | `interview_sessions`, `interview_questions`, `interview_responses`, `response_evaluations`, `interview_evaluations`, `ai_api_logs` |

Cross-schema foreign keys exist where referential integrity matters more than strict module isolation — particularly for `auth.users` (identity is foundational) and `recruitment.applications` (the pipeline spine).

If Module A needs data from Module B, it goes through a shared interface or listens for a domain event — not by querying Module B's tables directly. The schema boundary is a visible reminder of that rule.

---

## Key Design Decisions

**Modular monolith over microservices.** The core HR modules share a database, a deployment, and a runtime. This is simpler to develop, deploy, debug, and reason about than eight separate services with their own databases and network boundaries. The AI Interview Service is the one exception — its compute profile and deployment lifecycle are different enough to justify separation.

**Database-per-tenant over tenant ID filtering (for the monolith).** Full isolation at the database level eliminates an entire class of bugs (missing WHERE clauses leaking data). Per-tenant backup and compliance operations are straightforward. The scale (hundreds to low thousands of tenants, not millions) makes this operationally feasible.

**Custom auth over ASP.NET Identity.** Identity Framework's rigid table schema and UserManager abstractions fight against database-per-tenant multi-tenancy. Custom auth gives full control over schema, token flow, and tenant resolution integration.

**Domain events over direct module calls.** Modules publish events when something happens; interested modules react. This keeps the dependency graph clean — Recruitment doesn't know about Screening's internals, it just publishes "an application was submitted" and moves on.

**JSONB for flexible structured data.** Skills, social links, documents, settings, and scoring details are stored as JSONB. This avoids schema migrations for data that grows unpredictably (new social platforms, new skill categories) while still being queryable via GIN indexes where needed.

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
- [Recruitment DB](RECRUITMENT_DB_DESIGN.md) — Jobs, applications, client companies, skill requirements
- [Screening DB](SCREENING_DB_DESIGN.md) — Scoring, routing, skill match details
- [HR Workflows DB](HR_WORKFLOWS_DB_DESIGN.md) — Interviews, panels, offers
- [AI Interview DB](AI_INTERVIEW_DB_DESIGN.md) — Sessions, questions, responses, evaluations, API logs
- [CHECK Constraints](CHECK_CONSTRAINTS.md) — All enum constraints across the platform
