# D'Jobsite iConnect — Contributing Guide

> Coding and development standards for the D'Jobsite iConnect platform.
> Every contributor and AI agent must follow these conventions.

## How This Document Is Organized

This guide is **modular**. The root document (this file) defines the structure and links to focused standards documents. Each document covers one concern and can be referenced independently.

| Document                                          | Scope            | Description                                                                                         |
| ------------------------------------------------- | ---------------- | --------------------------------------------------------------------------------------------------- |
| [API Conventions](API_CONVENTIONS.md)             | All components   | HTTP endpoint design, headers, JWT authentication, request/response casing, pagination, validation  |
| [Error Envelope Specification](ERROR_ENVELOPE.md) | All components   | The canonical error response shape — one format, every component, no exceptions                     |
| [Database Conventions](DATABASE_CONVENTIONS.md)   | All components   | EF Core migrations, column types, timestamp handling, CHECK constraints, query patterns             |
| [.NET Conventions](DOTNET_CONVENTIONS.md)         | Modular monolith | Module structure, Clean Architecture, EF Core, Minimal API, DI, MediatR events, naming, Scalar docs |
| [Testing Standards](TESTING_STANDARDS.md)         | All components   | Test pyramid, architecture tests, integration tests, unit tests, naming conventions, test data      |

## System Components

| Component             | Language | Framework / Stack                                                      |
| --------------------- | -------- | ---------------------------------------------------------------------- |
| Modular Monolith      | C#       | .NET 10, Minimal API, EF Core, MediatR, FluentValidation               |
| — Tenancy module      | C#       | Catalog DB management, tenant provisioning, connection string cache    |
| — Auth module         | C#       | Custom JWT auth, OAuth (Google/Apple/Facebook), refresh token rotation |
| — Admin module        | C#       | Company settings (JSONB), audit logging, dashboard aggregation         |
| — Profiles module     | C#       | Applicant profiles, resume parsing, skills management                  |
| — Recruitment module  | C#       | Client companies, job postings, application intake                     |
| — Screening module    | C#       | Automated CV scoring, three-tier routing, threshold configuration      |
| — Matching module     | C#       | Candidate ranking, shortlist generation                                |
| — HR Workflows module | C#       | Final interviews, panel management, job offers                         |
| AI Interview Service  | Python   | FastAPI, SQLAlchemy, Alembic, OpenAI, aio-pika (RabbitMQ)              |

## Architecture at a Glance

**Modular monolith** with one **standalone microservice**.

- Eight modules share a runtime process and a per-tenant PostgreSQL database, isolated by schema.
- Modules communicate via **MediatR domain events** (in-process, synchronous).
- The AI Interview Service is deployed separately and communicates via **message broker** (RabbitMQ / Azure Service Bus).
- **Database-per-tenant** for the monolith. **Shared database with tenant ID filtering** for the AI Interview Service.

See [TECHNICAL_OVERVIEW.md](../TECHNICAL_OVERVIEW.md) for the full architecture diagram and design decisions.

## Core Principles

1. **Consistency over preference.** If a pattern is established, follow it — even if you'd do it differently on a greenfield project.
2. **Module boundaries are real.** Modules never query each other's tables directly. Communication is through domain events (MediatR) or, for the AI Interview Service, integration events (message broker).
3. **Schema = ownership boundary.** Each module owns its PostgreSQL schema. Cross-schema foreign keys exist only where referential integrity is critical (user identity, application spine).
4. **Test the boundaries.** Architecture tests enforce dependency direction. Integration tests verify infrastructure. Unit tests verify business logic.
5. **Flat, consistent, predictable.** Error envelopes, JSON casing, event shapes, header names — identical across the monolith and the AI Interview Service.

## Reading Order for New Contributors

1. Start with [TECHNICAL_OVERVIEW.md](../TECHNICAL_OVERVIEW.md) for the big picture — architecture, multi-tenancy, module responsibilities, event flow, application lifecycle.
2. Read [API Conventions](API_CONVENTIONS.md) and [Error Envelope Specification](ERROR_ENVELOPE.md) — these define the external contract.
3. Read [.NET Conventions](DOTNET_CONVENTIONS.md) for the modular monolith codebase.
4. Read [Database Conventions](DATABASE_CONVENTIONS.md) before writing any migrations or queries.
5. Read [Testing Standards](TESTING_STANDARDS.md) before writing any tests.
6. Read the relevant [database design doc](../database-designs/) for whichever module you're working on.
7. Read [CHECK_CONSTRAINTS.md](../database-designs/CHECK_CONSTRAINTS.md) before adding any status/enum column.

## For AI Agents

These documents serve as agent instructions. When working on any D'Jobsite iConnect component:

- Read this file first, then read the relevant linked documents before making changes.
- Do not introduce patterns that contradict these standards without explicit approval.
- When in doubt, look at existing code in the target module — if it matches these standards, follow the pattern. If it doesn't, follow the standard (the code has drifted).
- The convention docs take precedence over inferred patterns from the codebase.

## Dependency Direction (Enforced by Architecture Tests)

```
SharedKernel          ← no project references (except .NET SDK)
Module.Domain         ← SharedKernel only
Module.Application    ← Module.Domain only
Module.Infrastructure ← Module.Application (and transitively Domain)
Module.Api            ← Module.Application + Module.Infrastructure
Jobsite.Api           ← all Module.Api projects + SharedKernel
```

No module may reference another module's Domain, Application, or Infrastructure project. Cross-module communication is exclusively through events defined in SharedKernel.

## Key Design Decisions (Quick Reference)

| Decision           | Choice                            | Rationale                                                                     |
| ------------------ | --------------------------------- | ----------------------------------------------------------------------------- |
| Architecture       | Modular monolith + 1 microservice | Simpler than 8 microservices; AI service needs independent scaling            |
| Multi-tenancy      | Database-per-tenant (monolith)    | Full isolation, no tenant ID filter bugs, per-tenant backup/compliance        |
| Auth               | Custom (not ASP.NET Identity)     | Identity Framework fights database-per-tenant multi-tenancy                   |
| ORM                | EF Core                           | Fits modular monolith pattern; migrations, change tracking, schema management |
| Inter-module comms | MediatR domain events             | Clean dependency graph; publishers don't know about consumers                 |
| Enum storage       | String `VARCHAR(50)` + CHECK      | Readable in DB, queryable, no migration for reordering                        |
| Enum values        | PascalCase                        | Matches C# constants directly                                                 |
| Flexible data      | JSONB columns                     | Avoids migrations for skills, settings, social links                          |
| One-to-one         | Shared primary keys               | Enforces cardinality at DB level                                              |

Full rationale in [TECHNICAL_OVERVIEW.md](../TECHNICAL_OVERVIEW.md).
