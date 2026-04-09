# Project Guidelines

## Architecture

D'Jobsite iConnect is a **modular monolith** (C#/.NET 10) with one standalone **AI Service microservice** (Python/FastAPI).

- Eight modules share a runtime and a per-tenant PostgreSQL database, isolated by schema.
- Modules communicate via **domain events** (in-process event bus) — never direct table queries across module boundaries.
- The AI Service communicates via **HTTP for synchronous calls** (criteria suggestion, question suggestion) and **message broker** (RabbitMQ / Azure Service Bus) **for asynchronous calls** (resume parsing, AI screening, answer scoring, candidate feedback).
- **Database-per-tenant** for the monolith. **Shared database with tenant ID filtering** for the AI Service.

See `docs/TECHNICAL_OVERVIEW.md` for the full architecture.

## Conventions

All coding standards are documented in `docs/conventions/`. Read the relevant doc before making changes:

- `docs/conventions/CONTRIBUTING.md` — Start here. Links to all other docs.
- `docs/conventions/DOTNET_CONVENTIONS.md` — Module structure, EF Core, entities, error handling, endpoints, naming.
- `docs/conventions/API_CONVENTIONS.md` — Routes, JWT auth, headers, JSON casing, pagination, validation.
- `docs/conventions/DATABASE_CONVENTIONS.md` — EF Core migrations, column types, CHECK constraints, query patterns.
- `docs/conventions/ERROR_ENVELOPE.md` — Canonical error response shape.
- `docs/conventions/TESTING_STANDARDS.md` — Test pyramid, architecture tests, naming, integration patterns.

## Critical Rules

- **Never use `var`** — always use explicit types for all variable declarations.
- **`sealed class`** on all concrete classes unless inheritance is explicitly needed.
- **`CancellationToken ct`** as the last parameter on every async method, forwarded to every awaited call.
- **`AsNoTracking()`** on all EF Core read queries.
- **PascalCase** for enum/status values (matching CHECK constraints). **snake_case** for JSON, DB columns, cache keys.
- **`AppError` exceptions** for all domain errors — never return null where an error is expected. See `AppErrors` sentinels in SharedKernel.
- **No cross-module project references.** Modules communicate only through events in SharedKernel.

## Module Layer Dependencies

```
SharedKernel          ← no project references
Module.Domain         ← SharedKernel only
Module.Application    ← Module.Domain only
Module.Infrastructure ← Module.Application
Module.Api            ← Module.Application + Module.Infrastructure
```

## Build & Test

```bash
# Build
dotnet build jobsite-api/Jobsite.Api.slnx

# Test
dotnet test                                              # all tests
dotnet test --project jobsite-api/tests/Jobsite.UnitTests # unit only

# AI Service
cd ai-service && pytest
```

## Database Design Docs

Before modifying any module, read its database design: `docs/database-designs/{MODULE}_DB_DESIGN.md`

Before adding any status/enum column, check: `docs/database-designs/CHECK_CONSTRAINTS.md`
