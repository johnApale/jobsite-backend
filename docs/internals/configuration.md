# Configuration

All application configuration lives in `appsettings.json` (and environment overrides). The API uses the standard ASP.NET Core configuration system with strongly-typed option classes.

## Configuration files

| File                           | Purpose                                                          |
| ------------------------------ | ---------------------------------------------------------------- |
| `appsettings.json`             | Base configuration for all environments                          |
| `appsettings.Development.json` | Development overrides (more verbose logging, dev secrets)        |
| Environment variables          | Production secrets — override any key via `App__JwtSecret`, etc. |

## Sections

### `Logging`

Standard ASP.NET Core logging levels. Serilog reads these and also provides its own enrichment.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

In `Development`, EF Core command logging is promoted to `Information`:

```json
"Microsoft.EntityFrameworkCore.Database.Command": "Information"
```

### `ConnectionStrings`

| Key         | Description                                                                                      |
| ----------- | ------------------------------------------------------------------------------------------------ |
| `CatalogDb` | PostgreSQL connection string for the **catalog database** (tenant registry, platform-level data) |

```json
{
  "ConnectionStrings": {
    "CatalogDb": "Host=localhost;Port=5432;Database=djobsite_catalog;Username=postgres;Password=postgres"
  }
}
```

> **Note:** Tenant databases use per-tenant connection strings stored in the `Tenant` entity. The `CatalogDb` is the only connection string in config.

### `App`

Bound to the `AppSettings` class via `configuration.GetSection("App").Get<AppSettings>()`.

| Property                     | Type                    | Default                   | Description                                        |
| ---------------------------- | ----------------------- | ------------------------- | -------------------------------------------------- |
| `JwtSecret`                  | `string`                | `""`                      | HS256 signing key. **Must be ≥ 32 characters.**    |
| `JwtIssuer`                  | `string`                | `"djobsite-iconnect"`     | `iss` claim value                                  |
| `JwtAudience`                | `string`                | `"djobsite-iconnect"`     | `aud` claim value                                  |
| `JwtExpirationMinutes`       | `int`                   | `60`                      | Access token lifetime                              |
| `RefreshTokenExpirationDays` | `int`                   | `30`                      | Refresh token lifetime                             |
| `AiServiceUrl`               | `string`                | `"http://localhost:8000"` | Base URL for the AI Interview microservice         |
| `MessageBroker`              | `MessageBrokerSettings` | _(see below)_             | RabbitMQ / Azure Service Bus connection            |
| `Redis`                      | `RedisSettings`         | _(see below)_             | Redis connection (optional, for distributed cache) |

#### `MessageBrokerSettings`

| Property      | Type     | Default       | Description           |
| ------------- | -------- | ------------- | --------------------- |
| `Host`        | `string` | `"localhost"` | RabbitMQ host address |
| `Port`        | `int`    | `5672`        | RabbitMQ AMQP port    |
| `Username`    | `string` | `"guest"`     | RabbitMQ username     |
| `Password`    | `string` | `"guest"`     | RabbitMQ password     |
| `VirtualHost` | `string` | `"/"`         | RabbitMQ virtual host |

#### `RedisSettings`

| Property           | Type     | Default | Description                                           |
| ------------------ | -------- | ------- | ----------------------------------------------------- |
| `ConnectionString` | `string` | `""`    | Redis connection string. Empty = use in-memory cache. |

## Strongly-typed options class

**File:** `Configuration/AppSettings.cs`

```csharp
public sealed class AppSettings
{
    public const string SectionName = "App";

    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "djobsite-iconnect";
    public string JwtAudience { get; set; } = "djobsite-iconnect";
    public int JwtExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public string AiServiceUrl { get; set; } = "http://localhost:8000";
    public MessageBrokerSettings MessageBroker { get; set; } = new();
    public RedisSettings Redis { get; set; } = new();
}
```

## Serilog

Serilog is configured in `Program.cs` in two stages:

1. **Bootstrap logger** — console-only, used during startup before DI is ready.
2. **Full logger** — reads from `IConfiguration` and `IServiceProvider`, adds `FromLogContext` enrichment.

```csharp
builder.Host.UseSerilog((context, services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());
```

## Environment variable overrides

ASP.NET Core maps `__` (double underscore) to `:` in config keys. Examples:

```bash
export App__JwtSecret="production-secret-key-at-least-32-chars"
export ConnectionStrings__CatalogDb="Host=prod-db;Port=5432;Database=djobsite;..."
export App__AiServiceUrl="https://ai-interview.prod.djobsite.com"
export App__MessageBroker__Host="rabbitmq.prod.internal"
export App__MessageBroker__Username="djobsite"
export App__MessageBroker__Password="secret"
export App__Redis__ConnectionString="redis.prod.internal:6380,ssl=true"
export ASPNETCORE_ENVIRONMENT="Production"
```

## Tenant database connection strings

Tenant databases are **not** configured in `appsettings.json`. Each tenant's connection string is stored in the `tenants` table in the catalog database and built during provisioning:

```
Host={CatalogDb host};Port={CatalogDb port};Database=djobsite_tenant_{subdomain};Username={CatalogDb user};Password={CatalogDb password}
```

The database name follows the pattern `djobsite_tenant_{subdomain}` where `{subdomain}` is sanitized to alphanumeric characters and underscores only.

## Local development

A `docker-compose.yml` at the project root provides PostgreSQL and RabbitMQ for local development:

```bash
docker compose up -d
```

| Service    | Port(s)     | Credentials       |
| ---------- | ----------- | ----------------- |
| PostgreSQL | 5432        | postgres/postgres |
| RabbitMQ   | 5672, 15672 | guest/guest       |

RabbitMQ management UI is available at `http://localhost:15672`.
