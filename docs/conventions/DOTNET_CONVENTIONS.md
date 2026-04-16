# .NET Conventions

> Coding patterns, architecture, and style rules for the D'Jobsite iConnect modular monolith (.NET 10, C#).

## Module Structure (Clean Architecture)

Each of the eight modules follows the same four-layer structure:

```
Jobsite.Modules.{Module}.Domain/         # Entities, value objects, domain events, enums/constants
Jobsite.Modules.{Module}.Application/    # Services, DTOs, interfaces, validators
Jobsite.Modules.{Module}.Infrastructure/ # EF Core DbContext, repositories, external integrations
Jobsite.Modules.{Module}.Api/            # Minimal API endpoints, endpoint filters
```

**Dependency rules** (strictly enforced):

```
Domain         ← references only SharedKernel
Application    ← references Domain
Infrastructure ← references Application (and transitively Domain)
Api            ← references Application + Infrastructure
```

Domain and Application layers have **zero** infrastructure dependencies (no EF Core, no HTTP, no messaging). Infrastructure implements the interfaces defined in Application.

### SharedKernel

```
Jobsite.SharedKernel/
├── Domain/
│   ├── Entity.cs              # Base entity with Id, CreatedAt, UpdatedAt
│   ├── AggregateRoot.cs       # Base aggregate with domain event collection
│   ├── IDomainEvent.cs        # Marker interface for domain events
│   └── IIntegrationEvent.cs   # Marker interface for broker events
├── Errors/
│   ├── AppError.cs            # Typed exception class
│   └── AppErrors.cs           # Sentinel error instances
├── Results/
│   └── Result.cs              # Result<T> for error handling without exceptions
├── Events/
│   ├── ApplicationSubmittedEvent.cs
│   ├── CvScreeningCompletedEvent.cs
│   ├── CandidateReadyForInterviewEvent.cs
│   ├── CandidateShortlistedEvent.cs
│   ├── InterviewCompletedEvent.cs
│   ├── FinalInterviewScheduledEvent.cs
│   └── OfferExtendedEvent.cs
└── Persistence/
    └── IUnitOfWork.cs
```

### Composition Root (Jobsite.Api)

```
Jobsite.Api/
├── Program.cs                  # Host setup, middleware pipeline
├── Extensions/
│   └── ModuleServiceCollectionExtensions.cs  # Registers all module services
├── Middleware/
│   ├── CorrelationIdMiddleware.cs
│   ├── RequestLoggingMiddleware.cs
│   ├── AppErrorMiddleware.cs
│   ├── TenantResolutionMiddleware.cs
│   └── JwtAuthMiddleware.cs
├── Configuration/
│   └── AppSettings.cs
├── appsettings.json
├── appsettings.Development.json
└── appsettings.Production.json
```

### Test Structure

```
tests/
├── Jobsite.UnitTests/            # Pure unit tests (domain logic, services with mocked deps)
├── Jobsite.IntegrationTests/     # Testcontainers-based tests (DB, message broker)
└── Jobsite.ArchitectureTests/    # NetArchTest rules enforcing dependency direction
```

## Type Declarations

**Never use `var`**. Always use explicit types for all variable declarations — production and test code.

```csharp
// ✅ Correct
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
List<JobPosting> postings = await service.GetActivePostingsAsync(ct);
string connectionString = configuration.GetConnectionString("CatalogDb")!;
User? user = await userRepository.GetByIdAsync(id, ct);

// ❌ Wrong
var builder = WebApplication.CreateBuilder(args);
var postings = await service.GetActivePostingsAsync(ct);
```

## Program.cs

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfig) => { ... });

    AppSettings appSettings = builder.Configuration
        .GetSection(AppSettings.SectionName)
        .Get<AppSettings>() ?? new AppSettings();

    builder.Services.AddJobsiteModules(builder.Configuration);
    builder.Services.AddOpenApi(options => { ... });

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("D'Jobsite iConnect API");
            options.WithTheme(ScalarTheme.DeepSpace);
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });
    }

    // Middleware pipeline (order matters)
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<AppErrorMiddleware>();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();

    // Health
    app.MapHealthEndpoints();

    // Module endpoints
    app.MapTenancyEndpoints();
    app.MapAuthEndpoints();
    app.MapAdminEndpoints();
    app.MapProfileEndpoints();
    app.MapRecruitmentEndpoints();
    app.MapScreeningEndpoints();
    app.MapMatchingEndpoints();
    app.MapHRWorkflowEndpoints();

    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Application terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }

public partial class Program { }
```

Rules:

- `Program.cs` must stay clean — all DI registration lives in `ModuleServiceCollectionExtensions.AdjobsiteModules()`.
- JSON configuration (`snake_case`) is set in the extensions method, not in `Program.cs`.
- `public partial class Program { }` at the end for `WebApplicationFactory` test support.
- `TenantResolutionMiddleware` runs **before** authentication — it resolves the tenant DB context needed for user lookup.

## DI Registration

Each module exposes a single `Add{Module}Module()` extension method from its Infrastructure layer. The composition root aggregates them:

```csharp
public static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AdjobsiteModules(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Global JSON defaults (snake_case)
        // 2. Catalog DbContext (singleton connection to shared catalog DB)
        // 3. Tenant DbContext factory (per-request, resolved from tenant)
        // 4. Redis (singleton IConnectionMultiplexer)
        // 5. Domain event bus (scans all module assemblies)
        // 6. Message broker (RabbitMQ / Azure Service Bus)
        // 7. JWT authentication
        // 8. FluentValidation (scans all module assemblies)

        // Module registrations
        services.AddTenancyModule(configuration);
        services.AddAuthModule(configuration);
        services.AddAdminModule(configuration);
        services.AddProfilesModule(configuration);
        services.AddRecruitmentModule(configuration);
        services.AddScreeningModule(configuration);
        services.AddMatchingModule(configuration);
        services.AddHRWorkflowsModule(configuration);

        return services;
    }
}
```

Each module's `Add{Module}Module()` registers its own services:

```csharp
// In Jobsite.Modules.Auth.Infrastructure
public static class AuthModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories (scoped)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services (scoped)
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
```

Lifetime conventions:

- **Singleton:** `CatalogDbContext` factory, `IConnectionMultiplexer`, tenant cache
- **Scoped:** Tenant `DbContext`, all repositories, all application services, `IUnitOfWork`
- **Transient:** Validators, domain event handlers

## Domain Entities

```csharp
/// <summary>
/// Applicant professional identity — maps to the <c>profiles.applicant_profiles</c> table.
/// Shared primary key with <see cref="User"/> enforces one profile per user.
/// </summary>
public sealed class ApplicantProfile : Entity
{
    /// <summary>FK and shared PK — references <c>auth.users.id</c>.</summary>
    public Guid UserId { get; set; }

    /// <summary>Professional headline shown to recruiters.</summary>
    public string? ProfessionalSummary { get; set; }

    /// <summary>Parsed skills stored as JSONB. See profiles DB design for format.</summary>
    public List<Skill> Skills { get; set; } = [];

    /// <summary>Contact phone number.</summary>
    public string? PhoneNumber { get; set; }
}
```

Rules:

- `sealed class` — all entities.
- Inherit from `Entity` (SharedKernel base) for `Id`, `CreatedAt`, `UpdatedAt`.
- Aggregate roots inherit from `AggregateRoot` to collect domain events.
- `= null!` for non-nullable reference types that the DB populates.
- Expression-bodied computed properties (`=> ...`).
- Comprehensive XML doc comments (see Scalar API Documentation section).

## Status Constants

```csharp
/// <summary>
/// Lifecycle status constants for the <c>recruitment.applications.status</c> column.
/// Values must match the CHECK constraint <c>chk_applications_status</c> exactly.
/// </summary>
public static class ApplicationStatus
{
    public const string Submitted = "Submitted";
    public const string Screening = "Screening";
    public const string AiInterview = "AiInterview";
    public const string Shortlisted = "Shortlisted";
    public const string FinalInterview = "FinalInterview";
    public const string Offered = "Offered";
    public const string Hired = "Hired";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static bool IsValid(string status) =>
        status is Submitted or Screening or AiInterview or Shortlisted
              or FinalInterview or Offered or Hired or Rejected or Withdrawn;
}
```

Rules:

- `static class` with `const string` fields — not C# enums.
- Values are `PascalCase`, matching DB CHECK constraint values exactly.
- Include `IsValid()` method for validation.
- One status class per database table that has a status/enum column.

## Repositories (EF Core)

```csharp
public sealed class UserRepository : IUserRepository
{
    private readonly TenantDbContext _db;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(TenantDbContext db, ILogger<UserRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<IReadOnlyList<User>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Status == status)
            .ToListAsync(ct);
    }
}
```

Rules:

- All repos take `TenantDbContext` + `ILogger<T>` via constructor injection.
- `AsNoTracking()` on all read queries — tracked queries only when updating.
- `CancellationToken ct` forwarded to every EF Core call.
- `sealed class` — all repositories.
- Return `IReadOnlyList<T>` for collections via `.ToListAsync()`.
- Interfaces defined in the Application layer, implementations in Infrastructure.
- Complex queries use raw SQL via `FromSqlInterpolated` when LINQ becomes unreadable.

## EF Core DbContext

Each module configures its entities within the shared `TenantDbContext` using `IEntityTypeConfiguration<T>`:

```csharp
// In Jobsite.Modules.Auth.Infrastructure/Persistence/UserConfiguration.cs
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "auth");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Status).HasMaxLength(50).IsRequired();
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(u => new { u.Email }).IsUnique();
    }
}
```

Rules:

- Table names are `snake_case`, prefixed with schema: `builder.ToTable("table_name", "schema_name")`.
- Column name mapping handled globally via `NpgsqlSnakeCaseNamingConvention`.
- Each module owns its entity configurations — they are discovered via assembly scanning.
- The `TenantDbContext` is instantiated per-request with the tenant's connection string.

## Error Handling

### AppError

```csharp
public sealed class AppError : Exception
{
    public string Code { get; }
    public int StatusCode { get; }
    public object? Details { get; }

    public AppError(string code, int statusCode, string message, object? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    public AppError WithMessage(string message) => new(Code, StatusCode, message, Details);
    public AppError WithMessage(string format, params object[] args) =>
        new(Code, StatusCode, string.Format(format, args), Details);
    public AppError WithDetails(object details) => new(Code, StatusCode, Message, details);
}
```

### AppErrors (Sentinels)

```csharp
public static class AppErrors
{
    // Generic
    public static readonly AppError Validation = new("VALIDATION_ERROR", 400, "Request validation failed");
    public static readonly AppError Unauthorized = new("UNAUTHORIZED", 401, "Authentication required");
    public static readonly AppError Forbidden = new("FORBIDDEN", 403, "Insufficient permissions");
    public static readonly AppError NotFound = new("NOT_FOUND", 404, "Resource not found");
    public static readonly AppError InternalError = new("INTERNAL_ERROR", 500, "An unexpected error occurred");

    // Domain-specific (examples)
    public static readonly AppError TenantNotFound = new("TENANT_NOT_FOUND", 404, "Tenant not found");
    public static readonly AppError UserNotFound = new("USER_NOT_FOUND", 404, "User not found");
    public static readonly AppError ApplicationNotFound = new("APPLICATION_NOT_FOUND", 404, "Application not found");
    public static readonly AppError DuplicateApplication = new("DUPLICATE_APPLICATION", 409, "Application already exists for this job");
    public static readonly AppError DuplicateEmail = new("DUPLICATE_EMAIL", 400, "Email already registered");
    public static readonly AppError InvalidCredentials = new("INVALID_CREDENTIALS", 401, "Invalid email or password");
    public static readonly AppError TokenExpired = new("TOKEN_EXPIRED", 401, "Token has expired");
    public static readonly AppError TokenReplayDetected = new("TOKEN_REPLAY_DETECTED", 401, "Refresh token replay detected");
}
```

Rules:

- Throw `AppError` for all domain/business errors — never return null where an error is expected.
- Use `.WithMessage()` for custom messages, `.WithDetails()` for validation details.
- Do not catch exceptions in services/repos unless degrading gracefully.
- `AppErrorMiddleware` catches `AppError` and writes the standard error envelope.
- `AppError` and `AppErrors` live in SharedKernel so all modules can reference them.

## Endpoints (Minimal API)

```csharp
public static class JobPostingEndpoints
{
    public static RouteGroupBuilder MapJobPostingEndpoints(this RouteGroupBuilder routes)
    {
        RouteGroupBuilder group = routes.MapGroup("/job-postings");

        group.MapGet("/{id:guid}", async (
                Guid id,
                IJobPostingService service,
                CancellationToken ct) =>
            {
                JobPostingResponse posting = await service.GetByIdAsync(id, ct);
                return Results.Ok(posting);
            })
            .WithTags("JobPostings")
            .WithName("GetJobPostingById")
            .WithSummary("Get job posting by ID")
            .WithDescription("Retrieves a job posting by its unique identifier.")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }
}
```

Rules:

- Static extension methods on `RouteGroupBuilder` — not controllers.
- Group related endpoints with `MapGroup`.
- Module-level endpoint registration maps to `RouteGroupBuilder` from the corresponding route prefix.
- Every `Map*` call chains full metadata (see API Conventions).

## Middleware Pipeline

Standard order (must be consistent):

1. `CorrelationIdMiddleware` — read/generate correlation ID
2. `RequestLoggingMiddleware` — log request start
3. `AppErrorMiddleware` — catch `AppError`, write error envelope
4. `TenantResolutionMiddleware` — resolve tenant from subdomain, set up `TenantDbContext`
5. `UseAuthentication()` / `UseAuthorization()` — JWT validation
6. `UseSerilogRequestLogging()` — log request completion

## Domain Events (In-Process Event Bus)

Modules communicate via domain events published through the in-process event bus:

```csharp
// In Recruitment Domain
public sealed class ApplicationSubmittedEvent : IDomainEvent
{
    public Guid ApplicationId { get; init; }
    public Guid JobPostingId { get; init; }
    public Guid ApplicantUserId { get; init; }
    public Guid TenantId { get; init; }
}
```

```csharp
// In Screening Application — handles the event
public sealed class ApplicationSubmittedEventHandler
    : IDomainEventHandler<ApplicationSubmittedEvent>
{
    private readonly IScreeningService _screeningService;

    public ApplicationSubmittedEventHandler(IScreeningService screeningService)
    {
        _screeningService = screeningService;
    }

    public async Task HandleAsync(ApplicationSubmittedEvent domainEvent, CancellationToken ct)
    {
        await _screeningService.ScreenApplicationAsync(domainEvent.ApplicationId, ct);
    }
}
```

Rules:

- Domain events implement `IDomainEvent` (standalone marker interface).
- Events are dispatched after `SaveChangesAsync` via the `AggregateRoot` pattern.
- Handlers implement `IDomainEventHandler<T>` and live in the consuming module's Application layer.
- Integration events (cross-service, to AI Interview Service) implement `IIntegrationEvent` and are published to the message broker, not the in-process event bus.

## Naming Conventions

| Element             | Convention                      | Example                                            |
| ------------------- | ------------------------------- | -------------------------------------------------- |
| Methods             | PascalCase, async suffix        | `GetByIdAsync`, `CreateAsync`                      |
| Private fields      | `_camelCase`                    | `_db`, `_logger`, `_cache`                         |
| Parameters          | camelCase                       | `userId`, `tenantId`                               |
| `CancellationToken` | Always named `ct`               | `CancellationToken ct`                             |
| DB columns in SQL   | `snake_case`                    | `tenant_id`, `created_at`                          |
| Error codes         | `SCREAMING_SNAKE_CASE`          | `APPLICATION_NOT_FOUND`                            |
| Cache keys          | `snake_case:colon_separated`    | `tenant:{subdomain}`                               |
| JSON fields         | `snake_case` (global config)    | `professional_summary`, `tenant_id`                |
| Test methods        | `MethodName_Condition_Expected` | `CreateAsync_DuplicateEmail_ThrowsAppError`        |
| Namespaces          | Match project/folder structure  | `Jobsite.Modules.Auth.Infrastructure.Repositories` |
| DB table names      | `snake_case` (plural)           | `job_postings`, `applicant_profiles`               |
| DB schema names     | `snake_case`                    | `auth`, `recruitment`, `hr_workflows`              |
| Enum/status values  | `PascalCase`                    | `Submitted`, `AiInterview`, `FullTime`             |

## Cache Patterns

Two-tier caching: **HybridCache** (L1 in-memory + L2 Redis).

Cache keys are generated via static factory methods:

```csharp
public static class CacheKeys
{
    public static string Tenant(string subdomain) => $"tenant:{subdomain}";
    public static string TenantBranding(Guid tenantId) => $"tenant_branding:{tenantId}";
    public static string CompanySettings(Guid tenantId) => $"company_settings:{tenantId}";
}
```

Tenant resolution is the hottest path. The full tenant object is cached in Redis, keyed by subdomain. Cache invalidation only on tenant metadata or branding updates.

## Scalar API Documentation Standards

### XML Documentation

Every module `.csproj` must include:

```xml
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<NoWarn>$(NoWarn);1591</NoWarn>
```

Every public type, property, method, and interface must have XML doc comments:

- **Class level:** What DB table it maps to (`<c>table_name</c>`), its domain purpose.
- **Property level:** What the field represents, valid values/format, FK targets, nullability semantics, defaults.
- **Computed properties:** What condition they evaluate.
- **Constants:** When each state applies, state transitions at class level.
- **Error sentinels:** When the error is thrown.
- **Configuration:** What it configures, expected format, default value.
- **Middleware/Infrastructure:** Class role in pipeline, `<param>` tags for dependencies.

### Formatting Rules

- `<c>…</c>` for inline code (table names, column names, status values)
- `<see cref="…"/>` for cross-references
- `<paramref name="…"/>` for method parameters in summaries
- Summaries: one to two sentences max
- Do not restate the property name

## General Rules

- Always accept `CancellationToken ct` as the last parameter on any async method and forward it to every awaited call.
- Prefer `IReadOnlyList<T>` over `List<T>` or `IEnumerable<T>` for return types from repos and services.
- Never introduce new NuGet packages without stating the reason. Stack is intentionally minimal.
- Do not use `#region` blocks.
- Follow existing namespace → folder structure convention: `Jobsite.Modules.{Module}.{Layer}.{Subfolder}`.
- `sealed class` on all concrete classes unless inheritance is explicitly needed.
- Interfaces in Application layer, implementations in Infrastructure layer.
