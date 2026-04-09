# D'Jobsite iConnect

A multi-tenant, white-label HR hiring platform that takes companies from application intake to signed offer — with AI-assisted interviews along the way.

Built with .NET 10, PostgreSQL, and a modular monolith architecture. Each tenant (company or staffing agency) gets their own branded portal, their own database, and complete data isolation.

---

## What It Does

D'Jobsite iConnect provides an end-to-end hiring pipeline:

1. **Apply** — Candidates create profiles, upload resumes, and apply to jobs through a branded portal.
2. **Screen** — Resumes are automatically parsed, skills are matched against job requirements, and candidates are scored. Strong matches advance automatically; borderline cases get queued for human review.
3. **AI Interview** — Qualified candidates complete a digital interview with AI-generated questions tailored to the role. Responses (text, voice, or video) are scored automatically.
4. **Shortlist** — Screening and interview scores are combined to rank candidates. Top matches are shortlisted for the hiring team.
5. **Final Interview** — Human interviewers conduct in-person, video, or phone interviews with panel support and independent feedback.
6. **Offer** — Job offers are drafted, reviewed internally, extended to candidates, and tracked through acceptance or decline.

Every step is configurable per tenant — thresholds, scoring weights, question mix, required profile fields, and more.

---

## Who It's For

**Staffing agencies** that recruit on behalf of multiple client companies. They can post jobs anonymously ("Top Tech Company"), manage client relationships, and run separate pipelines per client.

**Companies hiring directly** that want a branded careers portal with automated screening and AI-assisted interviews to reduce time-to-hire.

**Platform operators** who manage the multi-tenant infrastructure, onboard new tenants, and monitor subscriptions.

---

## Key Capabilities

**Multi-tenancy with full data isolation.** Each tenant gets their own PostgreSQL database. No shared tables, no tenant ID filters on user data, no risk of cross-tenant leakage. Tenants are identified by subdomain (`acme.djobsite.com`) and resolved on every request.

**White-label branding.** Each tenant configures their own logo, colors, favicon, and tagline. The portal looks like their own product.

**Configurable hiring pipeline.** Tenants control screening thresholds, auto-advance/auto-reject behavior, scoring weights, AI interview question mix, required profile fields, and notification preferences — all from an admin panel.

**AI-powered interviews.** Questions are generated based on job requirements, scored by AI, and evaluated with per-category breakdowns (technical, communication, experience, behavioral). Supports text, voice, and video responses with automatic transcription.

**Agency support.** Client company management, anonymous job postings, and offer letters on behalf of external employers.

**Audit logging.** Every significant action is tracked with actor identity, timestamps, IP addresses, and structured context — designed to survive user deletion.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                     Modular Monolith                            │
│                                                                 │
│  ┌──────────┐ ┌──────┐ ┌───────────┐ ┌───────────┐ ┌────────┐ │
│  │ Tenancy  │ │ Auth │ │Recruitment│ │ Screening │ │Matching│ │
│  └──────────┘ └──────┘ └───────────┘ └───────────┘ └────────┘ │
│  ┌──────────────┐ ┌──────────┐ ┌───────┐                      │
│  │ HR Workflows │ │ Profiles │ │ Admin │                       │
│  └──────────────┘ └──────────┘ └───────┘                       │
└─────────────────────┬───────────────────────────────────────────┘
                      │ Message Broker
                      │ (RabbitMQ / Azure Service Bus)
                      │
       ┌──────────────▼──────────────┐
       │   AI Interview Service      │
       │   (separate microservice)   │
       └─────────────────────────────┘
```

The core HR logic lives in a **modular monolith** — eight modules sharing a runtime but isolated by code boundaries and database schemas. Modules communicate via in-process domain events.

The **AI Service** is a standalone microservice with its own database. It's separated because it has different scaling needs (compute-intensive AI calls) and a different deployment lifecycle. It communicates with the monolith through HTTP for synchronous operations (criteria suggestion, question suggestion) and a message broker for asynchronous operations (resume parsing, AI screening, answer scoring, candidate feedback).

---

## Databases

| Database            | Scope               | Purpose                                                                                                                      |
| ------------------- | ------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| **Catalog DB**      | One shared instance | Tenant metadata, subdomains, connection strings, branding, subscriptions. No user data.                                      |
| **Tenant DB**       | One per tenant      | Everything else — users, profiles, jobs, applications, screening, matching, interviews, offers, settings, audit logs.        |
| **AI Interview DB** | One shared instance | Interview sessions, questions, responses, evaluations, AI API logs. Uses tenant ID filtering instead of database-per-tenant. |

All monolith modules share the tenant database but own separate PostgreSQL schemas (`auth.*`, `profiles.*`, `recruitment.*`, `screening.*`, `matching.*`, `hr_workflows.*`, `admin.*`). Module boundaries are enforced in code — each module owns its tables and exposes data through interfaces and events.

---

## Tenant Onboarding

```
1. Company signs up via /api/tenants/register
2. Catalog DB entry created (subdomain, connection string, subscription tier)
3. New PostgreSQL database provisioned
4. EF Core migrations run against the new database
5. Initial AgencyAdmin user seeded from registration info
6. Default company settings created with sensible defaults
7. Tenant is live at {subdomain}.djobsite.com
```

---

## Authentication

Custom auth — not ASP.NET Identity. Supports email/password and OAuth (Google, Apple, Facebook).

- Tenants are resolved from subdomain on every request (cached in Redis)
- Users belong to exactly one tenant — their account exists only in that tenant's database
- Email uniqueness is per-tenant (same email can exist at different companies)
- JWTs carry tenant ID, user ID, and role claims
- Refresh token rotation with replay detection (family-based revocation)
- The JWT secret is shared between the monolith and AI Interview Service

---

## Roles

| Role              | Scope         | Access                                                       |
| ----------------- | ------------- | ------------------------------------------------------------ |
| **SystemAdmin**   | Platform-wide | Manage all tenants and subscriptions                         |
| **AgencyAdmin**   | Tenant        | Full admin — settings, users, all hiring data                |
| **HiringManager** | Tenant        | Manage jobs, view applicants, final decisions, extend offers |
| **Recruiter**     | Tenant        | Create jobs, manage applications, review screening results   |
| **Interviewer**   | Tenant        | Conduct and score final interviews                           |
| **Applicant**     | Tenant        | Apply to jobs, complete AI interviews, respond to offers     |

---

## Tech Stack

- **.NET 10** — Modular monolith + AI Interview microservice
- **PostgreSQL** — Database-per-tenant (monolith), shared database (AI service)
- **Entity Framework Core** — ORM with Npgsql provider
- **Redis** — Tenant resolution caching, rate limiting, session state
- **RabbitMQ / Azure Service Bus** — Cross-service messaging
- **In-process event bus** — Domain events between modules (hand-rolled, no external library)
- **JWT** — Shared token format across monolith and AI service
- **BCrypt** — Password hashing
- **AI Provider SDK** — OpenAI, Anthropic, or Azure OpenAI (swappable)

---

## Project Structure

```
DJobsite.iConnect/
├── src/
│   ├── DJobsite.Api/                    # API host — composition root, middleware, config
│   ├── DJobsite.SharedKernel/           # Shared contracts, events, base types (no business logic)
│   ├── Modules/
│   │   ├── Tenancy/                     # Tenant provisioning, subdomain resolution
│   │   ├── Auth/                        # Login, registration, JWT, refresh tokens, OAuth
│   │   ├── Admin/                       # Company settings, audit logs, dashboards
│   │   ├── Recruitment/                 # Job postings, applications, client companies
│   │   ├── Screening/                   # CV parsing, skill matching, scoring, routing
│   │   ├── Matching/                    # Candidate ranking, shortlist generation
│   │   ├── HRWorkflows/                 # Final interviews, panel feedback, job offers
│   │   └── Profiles/                    # Applicant profiles, resumes, skills, documents
│   └── Services/
│       └── DJobsite.AIInterview.Service/  # Standalone AI interview microservice
└── tests/
    ├── DJobsite.UnitTests/
    ├── DJobsite.IntegrationTests/
    └── DJobsite.AIInterview.Tests/
```

Every module follows the same internal layout: **Domain/** (entities), **Application/** (services, DTOs, events), **Infrastructure/** (persistence, integrations), **Api/** (controllers). Modules reference `SharedKernel` for contracts but never reference each other directly.

---

## Event-Driven Pipeline

The hiring pipeline is orchestrated through domain events (in-process) and integration events (cross-service):

```
Application Submitted
  → [domain event] → Screening begins
      → Screening complete
          → [domain event] → Matching receives scores
          → [integration event → broker] → AI Interview Service generates interview
              → Interview complete
                  → [integration event → broker] → Matching receives interview scores
                      → Candidate shortlisted
                          → [domain event] → HR Workflows schedules final interview
                              → Offer extended → Hired / Rejected
```

---

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL 15+
- Redis
- RabbitMQ (or Azure Service Bus connection string)
- AI provider API key (OpenAI, Anthropic, or Azure OpenAI)

### Configuration

1. Clone the repository
2. Copy `appsettings.json` to `appsettings.Development.json` in both `DJobsite.Api` and `DJobsite.AIInterview.Service`
3. Configure connection strings for the catalog database, Redis, and the message broker
4. Set the JWT secret (must match between monolith and AI service)
5. Add your AI provider API key to the AI Interview Service config

### Running

```bash
# Start the monolith
cd src/DJobsite.Api
dotnet run

# Start the AI Interview Service (separate terminal)
cd src/Services/DJobsite.AIInterview.Service
dotnet run
```

### First Tenant

```bash
# Register a new tenant
POST /api/tenants/register
{
  "name": "Acme Corp",
  "subdomain": "acme",
  "ownerName": "Jane Admin",
  "ownerEmail": "jane@acme.com"
}

# The tenant's portal is now live at acme.djobsite.com
# Jane can log in with the seeded credentials and start configuring
```

---

## Documentation

| Document                                            | Description                                                            |
| --------------------------------------------------- | ---------------------------------------------------------------------- |
| [Technical Overview](TECHNICAL_OVERVIEW.md)         | Architecture, module responsibilities, data flow, and design decisions |
| [Catalog DB Design](CATALOG_DB_DESIGN.md)           | Shared catalog database schema                                         |
| [Auth DB Design](AUTH_DB_DESIGN.md)                 | Authentication, OAuth, refresh tokens                                  |
| [Admin DB Design](ADMIN_DB_DESIGN.md)               | Company settings, audit logging                                        |
| [Profiles DB Design](PROFILES_DB_DESIGN.md)         | Applicant profiles, resumes, skills                                    |
| [Recruitment DB Design](RECRUITMENT_DB_DESIGN.md)   | Job postings, applications, client companies                           |
| [Screening DB Design](SCREENING_DB_DESIGN.md)       | CV screening, scoring, routing                                         |
| [HR Workflows DB Design](HR_WORKFLOWS_DB_DESIGN.md) | Final interviews, offers                                               |
| [AI Interview DB Design](AI_INTERVIEW_DB_DESIGN.md) | AI interview microservice database                                     |
| [CHECK Constraints](CHECK_CONSTRAINTS.md)           | All database enum constraints                                          |

---

## License

Proprietary. All rights reserved.
