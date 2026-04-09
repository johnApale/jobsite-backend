# Database Conventions

> Migration strategy, column types, SQL patterns, and schema conventions for D'Jobsite iConnect.

## Database Engine

**PostgreSQL 16** (Alpine image for local development).

### Database Topology

| Database        | Scope                  | Managed By           | Purpose                                                   |
| --------------- | ---------------------- | -------------------- | --------------------------------------------------------- |
| Catalog DB      | Single shared instance | Tenancy module       | Tenant metadata, subdomains, connection strings, branding |
| Tenant DB       | One per tenant         | Tenancy module       | All user data, hiring pipeline, settings (8 schemas)      |
| AI Interview DB | Single shared instance | AI Interview Service | Interview sessions, questions, responses, evaluations     |

There are no cross-database foreign keys. The monolith's modules share a single per-tenant database, isolated by PostgreSQL schema.

## Migrations (EF Core)

The monolith uses **EF Core migrations**. Each module contributes entity configurations to the shared `TenantDbContext`, and migrations are generated from the composition root.

### Naming

Migration names use PascalCase with a descriptive action:

```
{Timestamp}_Create{Module}{Table}.cs
{Timestamp}_Add{Column}To{Table}.cs
{Timestamp}_Add{Index}Index.cs
```

Examples:

```
20260401120000_CreateAuthUsers.cs
20260401120001_CreateAuthRefreshTokens.cs
20260401130000_CreateRecruitmentJobPostings.cs
20260402100000_AddEmailIndexToUsers.cs
```

### Generation

```bash
# From the Jobsite.Api project directory
dotnet ef migrations add CreateAuthUsers --project ../Modules/Auth/Jobsite.Modules.Auth.Infrastructure
```

### Schema Creation

Each module's entities map to tables under their owned schema. EF Core creates schemas automatically via `builder.ToTable("table_name", "schema_name")`.

### Tenant Provisioning

When a new tenant is registered, the Tenancy module:

1. Creates a new PostgreSQL database
2. Runs all EF Core migrations against it (creating all 8 schemas and their tables)
3. Seeds default `company_settings` and the initial admin user

### AI Interview Service Migrations

The AI Interview Service (Python) uses **Alembic** for its own database:

```bash
alembic revision --autogenerate -m "create interview sessions"
alembic upgrade head
```

## Column Types

| Concept                    | PostgreSQL Type                      | EF Core Config                                      |
| -------------------------- | ------------------------------------ | --------------------------------------------------- |
| Primary key                | `UUID DEFAULT gen_random_uuid()`     | `.HasDefaultValueSql("gen_random_uuid()")`          |
| Foreign key (same schema)  | `UUID REFERENCES schema.table(id)`   | `.HasForeignKey(e => e.FkId)`                       |
| Foreign key (cross-schema) | `UUID NOT NULL`                      | `.IsRequired()` + index (no FK constraint)          |
| Status/enum                | `VARCHAR(50)` with `CHECK`           | `.HasMaxLength(50)` + CHECK constraint in migration |
| Email                      | `VARCHAR(255)`                       | `.HasMaxLength(255)`                                |
| Short text                 | `VARCHAR(N)`                         | `.HasMaxLength(N)`                                  |
| Long text                  | `TEXT`                               | Default (no max length)                             |
| Boolean                    | `BOOLEAN NOT NULL DEFAULT false`     | `.HasDefaultValue(false)`                           |
| Money/amounts              | `NUMERIC(10,2)`                      | `.HasPrecision(10, 2)`                              |
| JSON data                  | `JSONB`                              | `.HasColumnType("jsonb")`                           |
| File size                  | `BIGINT`                             |                                                     |
| Timestamps                 | `TIMESTAMPTZ NOT NULL DEFAULT NOW()` | `.HasDefaultValueSql("NOW()")`                      |

## Timestamp Handling

### Column Type

**Always `TIMESTAMPTZ`** (timestamp with time zone). Stores the value in UTC internally.

```csharp
// In Entity base class (SharedKernel)
public DateTime CreatedAt { get; set; }
public DateTime UpdatedAt { get; set; }
```

```csharp
// In base entity configuration
builder.Property(e => e.CreatedAt)
    .HasDefaultValueSql("NOW()")
    .ValueGeneratedOnAdd();

builder.Property(e => e.UpdatedAt)
    .HasDefaultValueSql("NOW()")
    .ValueGeneratedOnAddOrUpdate();
```

### Auto-Update Trigger

Every table with an `updated_at` column must have the `update_updated_at_column()` trigger. Define once per database in the initial migration:

```sql
-- In the first migration's Up() method via migrationBuilder.Sql(...)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
```

Applied per table:

```sql
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON auth.users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
```

## CHECK Constraints

Status and enum columns use `VARCHAR(50)` with inline `CHECK` constraints — not PostgreSQL `ENUM` types.

```sql
ALTER TABLE recruitment.applications
  ADD CONSTRAINT chk_applications_status
  CHECK (status IN ('Submitted', 'Screening', 'AiInterview', 'Shortlisted',
                     'FinalInterview', 'Offered', 'Hired', 'Rejected', 'Withdrawn'));
```

Values are **PascalCase** — matching the C# `const string` definitions in status constant classes. See [CHECK_CONSTRAINTS.md](../database-designs/CHECK_CONSTRAINTS.md) for the complete reference.

**Three enforcement layers:**

1. **Database** — CHECK constraints. Last line of defense.
2. **Application** — C# status constant classes with `IsValid()`. Returns friendly 400 errors.
3. **API** — `GET /api/v1/lookups/{type}` endpoints for frontend dropdowns.

Adding a new enum value requires: update the C# constant class → add a migration to `ALTER` the CHECK constraint → deploy both together.

## Indexing

- Add indexes on columns used in `WHERE` clauses for common queries.
- Always index foreign key columns.
- Use partial indexes for status-filtered queries:

```sql
CREATE INDEX idx_applications_active ON recruitment.applications (job_posting_id)
    WHERE status NOT IN ('Rejected', 'Withdrawn');
```

- Unique constraints should be named: `{schema}_{table}_{column(s)}_unique`

```sql
ALTER TABLE auth.users ADD CONSTRAINT auth_users_email_unique
    UNIQUE (email);
```

- Composite indexes for the AI Interview Service always lead with `tenant_id`:

```sql
CREATE INDEX idx_interview_sessions_tenant_status
    ON ai_interview.interview_sessions (tenant_id, status);
```

## Cross-Schema References (No Foreign Keys)

Modules **never** create database-level foreign keys to tables in other schemas. Cross-module columns store a `UUID NOT NULL` value and are indexed, but have **no FK constraint**.

Referential integrity across module boundaries is enforced at the **application layer** through:

1. **SharedKernel reader interfaces** — Read-only contracts (e.g., `IApplicantDataReader`, `IScreeningScoreReader`) that return snapshot DTOs. Each implementation queries only its own module's `DbContext`.
2. **Domain events** — Modules react to events like `ApplicationSubmittedEvent` and `CvScreeningCompletedEvent`. The producing module guarantees its data exists before publishing.
3. **Status updater interfaces** — E.g., `IApplicationStatusUpdater` lets downstream modules update the source record's status through a SharedKernel contract.

**Why no DB-level FKs?** Cross-schema FK constraints couple module schemas at the database level, preventing independent module extraction. Application-layer integrity via SharedKernel preserves modular boundaries.

### Cross-Schema Reference Columns

These columns reference rows in other schemas but have **no FK constraint**:

| Column           | Source Table                    | Logical Reference              |
| ---------------- | ------------------------------- | ------------------------------ |
| `user_id`        | `profiles.applicant_profiles`   | `auth.users(id)`               |
| `applicant_id`   | `recruitment.applications`      | `auth.users(id)`               |
| `application_id` | `screening.screening_results`   | `recruitment.applications(id)` |
| `application_id` | `matching.candidate_matches`    | `recruitment.applications(id)` |
| `application_id` | `hr_workflows.final_interviews` | `recruitment.applications(id)` |
| `application_id` | `hr_workflows.job_offers`       | `recruitment.applications(id)` |
| `job_posting_id` | `matching.shortlists`           | `recruitment.job_postings(id)` |

Always add an **index** on cross-schema reference columns for query performance.

## Shared Primary Keys (One-to-One)

Tables with a one-to-one logical relationship to a parent in another schema use a shared primary key — the PK column holds the same UUID as the parent row. There is **no FK constraint**; the relationship is logical only.

```csharp
// screening_results.application_id is both PK and logical reference to recruitment.applications
builder.HasKey(sr => sr.ApplicationId);
builder.Property(sr => sr.ApplicationId)
    .ValueGeneratedNever(); // Set externally — matches the parent row's ID
```

This applies to: `applicant_profiles` (→ `auth.users`), `screening_results` (→ `recruitment.applications`), `candidate_matches` (→ `recruitment.applications`), `final_interviews` (→ `recruitment.applications`), `job_offers` (→ `recruitment.applications`).

## Query Patterns (EF Core)

```csharp
// Read (no tracking)
User? user = await _db.Users
    .AsNoTracking()
    .FirstOrDefaultAsync(u => u.Id == id, ct);

// Read with includes
Application application = await _db.Applications
    .AsNoTracking()
    .Include(a => a.ScreeningResult)
    .FirstOrDefaultAsync(a => a.Id == id, ct)
    ?? throw AppErrors.ApplicationNotFound;

// Write
_db.Applications.Add(application);
await _db.SaveChangesAsync(ct);

// Update
Application application = await _db.Applications
    .FirstOrDefaultAsync(a => a.Id == id, ct)
    ?? throw AppErrors.ApplicationNotFound;
application.Status = ApplicationStatus.Screening;
await _db.SaveChangesAsync(ct);

// Raw SQL when LINQ is unreadable
IReadOnlyList<ScreeningResult> results = await _db.ScreeningResults
    .FromSqlInterpolated($"""
        SELECT sr.* FROM screening.screening_results sr
        JOIN recruitment.applications a ON a.id = sr.application_id
        WHERE a.job_posting_id = {jobPostingId}
        AND sr.status = {ScreeningStatus.Completed}
        ORDER BY sr.overall_score DESC
        """)
    .AsNoTracking()
    .ToListAsync(ct);
```

Rules:

- `AsNoTracking()` on all read queries.
- Tracked queries only when updating entities.
- `CancellationToken ct` on every EF Core call.
- Use `Include()` for eager loading — no lazy loading.
- Raw SQL via `FromSqlInterpolated` (parameterized) — never string concatenation.

## SQL Style (Raw SQL and Migrations)

- Keywords: `UPPERCASE` (`SELECT`, `FROM`, `WHERE`, `INSERT INTO`, `RETURNING`)
- Table/column names: `snake_case` (`tenant_id`, `created_at`)
- Schema-qualified table names in raw SQL: `auth.users`, `recruitment.applications`
- Use aliases for readability in joins: `FROM auth.users u JOIN recruitment.applications a ON a.user_id = u.id`

## Connection Pooling

Npgsql internally pools connections. The `TenantDbContext` is scoped per-request — a new context is created with the resolved tenant's connection string and disposed at the end of the request.

The Catalog DB uses a singleton `DbContextFactory` since it has a fixed connection string.
