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

### 4. MediatR

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<CatalogDbContext>();
});
```

Scans module assemblies for `IRequestHandler<,>` and `INotificationHandler<>` implementations. Add new module assemblies here as they are implemented.

### 5. Module registrations

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
3. If the module has handlers, register its assembly with MediatR.
4. Map endpoints in `Program.cs` via `app.Map{Module}Endpoints()`.
