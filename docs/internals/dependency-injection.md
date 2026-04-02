# Dependency Injection

All DI wiring goes through a single extension method in `Extensions/ModuleServiceCollectionExtensions.cs`, called from `Program.cs`:

```csharp
builder.Services.AddJobsiteModules(builder.Configuration);
```

## Registration order

`AddJobsiteModules` registers services in this sequence:

### 1. Global JSON defaults

```csharp
services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
```

- All minimal API responses use **snake_case** property names.
- Null properties are **omitted** from JSON output.

### 2. AppSettings binding

```csharp
AppSettings appSettings = configuration
    .GetSection(AppSettings.SectionName)
    .Get<AppSettings>() ?? new AppSettings();
```

See [configuration.md](configuration.md) for the full options shape.

### 3. JWT authentication

Configures `JwtBearerDefaults.AuthenticationScheme` with:

| Parameter                  | Value                                                       |
| -------------------------- | ----------------------------------------------------------- |
| `ValidateIssuer`           | `true`                                                      |
| `ValidateAudience`         | `true`                                                      |
| `ValidateLifetime`         | `true`                                                      |
| `ValidateIssuerSigningKey` | `true`                                                      |
| `ValidIssuer`              | `AppSettings.JwtIssuer`                                     |
| `ValidAudience`            | `AppSettings.JwtAudience`                                   |
| `IssuerSigningKey`         | `SymmetricSecurityKey` from `AppSettings.JwtSecret` (HS256) |
| `ClockSkew`                | `TimeSpan.Zero` (strict expiry)                             |

Also registers `services.AddAuthorization()`.

### 4. HttpContextAccessor

```csharp
services.AddHttpContextAccessor();
```

Required by `ITenantDbContextFactory` to resolve the tenant connection string from the current HTTP request context (`HttpContext.Items["TenantConnectionString"]`).

### 5. MediatR + Pipeline Behaviors

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CatalogDbContext>();
    cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
});
```

Scans module assemblies for `IRequestHandler<,>` and `INotificationHandler<>`. Registers two open generic pipeline behaviors:

| Behavior                     | Purpose                                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------------------- |
| `LoggingPipelineBehavior`    | Logs request name, start/finish, and elapsed time via `Stopwatch`                                        |
| `ValidationPipelineBehavior` | Runs all `IValidator<TRequest>` from DI; throws `AppErrors.Validation` with per-field details on failure |

Pipeline behaviors execute in registration order: logging wraps validation wraps the handler.

### 6. FluentValidation

```csharp
services.AddValidatorsFromAssemblyContaining<CatalogDbContext>(includeInternalTypes: true);
```

Registers all `IValidator<T>` implementations from module assemblies. Validators are consumed by `ValidationPipelineBehavior`.

### 7. Domain event dispatcher

```csharp
services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
```

Bridges `TenantDbContext` (SharedKernel) → MediatR. The `TenantDbContext.SaveChangesAsync()` collects domain events from aggregate roots and dispatches them via this interface after persistence succeeds. Defined in SharedKernel so the DbContext doesn't depend on the full MediatR package.

### 8. MassTransit + RabbitMQ

```csharp
services.AddMassTransit(bus =>
{
    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(appSettings.MessageBroker.Host, ...);
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            return options;
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

MassTransit is configured with **snake_case JSON serialization** for Python AI Service interop (Pydantic expects `event_id`, not `EventId`). Consumers are registered from module assemblies as they are implemented.

### 9. Integration event publisher

```csharp
services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
```

Wraps MassTransit's `IPublishEndpoint` to publish `IIntegrationEvent` instances to the message broker. Modules depend on the `IEventPublisher` interface from SharedKernel, not on MassTransit directly.

### 10. Module registrations

Each module exposes an `Add{Module}Module(IConfiguration)` extension method that registers its own DbContext, repositories, and services.

```csharp
services.AddTenancyModule(configuration);    // ← implemented
// services.AddAuthModule(configuration);     // ← skeleton
// services.AddAdminModule(configuration);
// services.AddProfilesModule(configuration);
// services.AddRecruitmentModule(configuration);
// services.AddScreeningModule(configuration);
// services.AddMatchingModule(configuration);
// services.AddHRWorkflowsModule(configuration);
```

#### Tenancy module registrations

`AddTenancyModule` registers:

| Service              | Implementation      | Lifetime  | Purpose                                    |
| -------------------- | ------------------- | --------- | ------------------------------------------ |
| `CatalogDbContext`   | _(EF Core)_         | Scoped    | Catalog database access                    |
| `ITenantRepository`  | `TenantRepository`  | Scoped    | Tenant CRUD operations                     |
| `IUnitOfWork`        | `CatalogUnitOfWork` | Scoped    | Catalog DB transaction boundary            |
| `IMemoryCache`       | _(framework)_       | Singleton | Backing store for tenant cache             |
| `ITenantCache`       | `MemoryTenantCache` | Singleton | Cache-first tenant lookup (5-min sliding)  |
| `ITenantProvisioner` | `TenantProvisioner` | Scoped    | CREATE DATABASE + tenant status management |
| `ITenantService`     | `TenantService`     | Scoped    | Application service for tenant operations  |

## TenantDbContext per-request lifetime

Module-level `TenantDbContext` subclasses (e.g., `AuthDbContext`, `RecruitmentDbContext`) are **not** registered in DI directly. Instead, modules register an `ITenantDbContextFactory<TContext>` that creates a context per request:

- **HTTP requests**: The factory reads `HttpContext.Items["TenantConnectionString"]` (set by `TenantResolutionMiddleware`) and builds a DbContext with that connection string.
- **MassTransit consumers / background jobs**: The factory's second overload accepts an explicit connection string, resolved by looking up the tenant ID from the event payload in the catalog database.

```csharp
// HTTP handler
AuthDbContext db = authDbContextFactory.CreateDbContext();

// MassTransit consumer
AuthDbContext db = authDbContextFactory.CreateDbContext(tenantConnectionString);
```

## OpenAPI services

Registered separately in `Program.cs` (not in `AddJobsiteModules`):

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<ErrorSchemaTransformer>();
    options.AddDocumentTransformer((document, _, _) => { /* API info */ });
});
```

| Transformer                       | Purpose                                               |
| --------------------------------- | ----------------------------------------------------- |
| `BearerSecuritySchemeTransformer` | Adds JWT Bearer security scheme + global requirement  |
| `ErrorSchemaTransformer`          | Adds `ErrorEnvelope` schema to all error status codes |
| Inline document transformer       | Sets API title, version, description, contact         |

## Adding a new module

1. Create the `Add{Module}Module` extension in the module's `Infrastructure` project.
2. Uncomment / add the call in `AddJobsiteModules`.
3. If the module has MediatR handlers, add its assembly to `cfg.RegisterServicesFromAssemblyContaining<>()`.
4. If the module has FluentValidation validators, add its assembly to `AddValidatorsFromAssemblyContaining<>()`.
5. If the module has MassTransit consumers, add them via `bus.AddConsumers(typeof(ModuleMarker).Assembly)`.
6. Map endpoints in `Program.cs` via `app.Map{Module}Endpoints()`.
