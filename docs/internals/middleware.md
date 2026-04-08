# Middleware Pipeline

The middleware pipeline is configured in `Program.cs`. **Order matters** — each middleware runs in the order it is registered.

## Pipeline order

```
Request
  ┌─────────────────────────────────┐
  │ 1. CorrelationIdMiddleware      │ ← assigns / reads X-Correlation-ID
  │ 2. RequestLoggingMiddleware     │ ← logs request start + response status
  │ 3. AppErrorMiddleware           │ ← catches AppError + unhandled exceptions
  │ 4. TenantResolutionMiddleware   │ ← resolves tenant from subdomain
  │ 5. UseAuthentication()          │ ← validates JWT Bearer token
  │ 6. UseAuthorization()           │ ← enforces [Authorize] policies
  │ 7. UseRateLimiter()             │ ← enforces per-endpoint rate limiting
  │ 8. UseSerilogRequestLogging()   │ ← Serilog enriched request log
  └─────────────────────────────────┘
Response
```

Registration in `Program.cs`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<AppErrorMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseSerilogRequestLogging();
```

---

## 1. CorrelationIdMiddleware

**File:** `Middleware/CorrelationIdMiddleware.cs`

| Aspect    | Detail                                                             |
| --------- | ------------------------------------------------------------------ |
| Purpose   | Ensures every request has a correlation ID for distributed tracing |
| Header    | `X-Correlation-ID`                                                 |
| Behaviour | Reads from inbound header; generates a UUID if absent              |
| Storage   | `HttpContext.Items["CorrelationId"]`                               |
| Response  | Echoes the correlation ID back on the response header              |

---

## 2. RequestLoggingMiddleware

**File:** `Middleware/RequestLoggingMiddleware.cs`

| Aspect    | Detail                                                                           |
| --------- | -------------------------------------------------------------------------------- |
| Purpose   | Structured logging of every request/response pair                                |
| Log start | `HTTP {Method} {Path} started [CorrelationId=…]`                                 |
| Log end   | `HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId=…]` |
| Timer     | Uses `Stopwatch.GetTimestamp()` / `GetElapsedTime()` for high-resolution timing  |
| Logger    | `Serilog.Log.Information()`                                                      |

---

## 3. AppErrorMiddleware

**File:** `Middleware/AppErrorMiddleware.cs`

Catches two exception types and serialises them into the [canonical error envelope](../api-reference/errors.md):

### `AppError` exceptions

Domain errors thrown via `AppErrors` sentinels. The middleware:

1. Reads the correlation ID from `HttpContext.Items`.
2. Sets the HTTP status code from `AppError.StatusCode`.
3. Writes a JSON body with `code`, `message`, `details` (optional), and `request_id`.

### Unhandled exceptions

Any other exception is caught and returned as:

```json
{
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred",
  "request_id": "<correlation-id>"
}
```

with HTTP 500. The original exception is **not** leaked to the client.

### JSON serialisation

Uses a dedicated `JsonSerializerOptions` instance with `SnakeCaseLower` naming and `WhenWritingNull` ignore condition, matching the global API convention.

---

## 4. TenantResolutionMiddleware

**File:** `Middleware/TenantResolutionMiddleware.cs`

Resolves the current tenant **before** authentication so the tenant's database context is available for user lookup.

### Resolution flow

```
Host header → extract subdomain → check cache → look up tenant in DB → cache result → validate status → store in HttpContext
```

1. **Extract subdomain** from `Host` header (e.g. `acme.djobsite.com` → `acme`).
2. **Skip** if the route is a non-tenant route (see bypass list below).
3. **Check cache** via `ITenantCache.GetBySubdomainAsync()` for a cached tenant.
4. **On cache miss**, look up the tenant by subdomain via `ITenantRepository.GetBySubdomainAsync()`, then populate the cache via `ITenantCache.SetAsync()`.
5. **Validate** the tenant's status is `Active`. Reject `Suspended` / `Deactivated` / `Provisioning` / `ProvisioningFailed` tenants with 403.
6. **Store** in `HttpContext.Items`:
   - `"Tenant"` → the `Tenant` entity
   - `"TenantConnectionString"` → the tenant's database connection string

### Tenant caching

The middleware uses `ITenantCache` (injected per-request from DI) to avoid hitting the database on every request. The default `MemoryTenantCache` implementation uses `IMemoryCache` with a 5-minute sliding expiration.

| Aspect             | Detail                                                                   |
| ------------------ | ------------------------------------------------------------------------ |
| Cache key          | `tenant:subdomain:{subdomain}`                                           |
| Expiration         | 5-minute sliding window                                                  |
| Implementation     | `MemoryTenantCache` (singleton via `IMemoryCache`)                       |
| Cache invalidation | `ITenantCache.InvalidateAsync(subdomain)`                                |
| Upgrade path       | Swap to Redis-backed implementation when `Redis.ConnectionString` is set |

### Bypass routes

These paths skip tenant resolution entirely:

| Path prefix       | Reason                            |
| ----------------- | --------------------------------- |
| `/health`         | Health checks (no tenant context) |
| `/ready`          | Readiness probe                   |
| `/api/v1/tenants` | Platform-level tenant management  |
| `/scalar`         | Scalar API docs UI                |
| `/openapi`        | OpenAPI spec endpoint             |

### Error responses

| Scenario                        | Status | Code               |
| ------------------------------- | ------ | ------------------ |
| No subdomain (e.g. `localhost`) | 400    | `INVALID_REQUEST`  |
| Subdomain not found in DB       | 404    | `TENANT_NOT_FOUND` |
| Tenant is suspended/deactivated | 403    | `FORBIDDEN`        |

---

## 7. UseRateLimiter

Built-in ASP.NET Core rate limiting middleware. Policies are defined in `ModuleServiceCollectionExtensions.cs` and applied to endpoint groups via `.RequireRateLimiting()`.

### Policies

| Policy     | Partition key | Limit            | Window | Applied to                                                    |
| ---------- | ------------- | ---------------- | ------ | ------------------------------------------------------------- |
| `"auth"`   | Client IP     | 10 requests/min  | Fixed  | Auth endpoints (`/api/v1/auth`)                               |
| `"ai"`     | Tenant ID     | 20 requests/min  | Fixed  | AI suggestion endpoints (criteria suggest, questions suggest) |
| `"global"` | Tenant ID     | 100 requests/min | Fixed  | All other endpoint groups                                     |

Limits are configurable via `appsettings.json` under the `RateLimiting` section (`GlobalRequestsPerMinute`, `AuthRequestsPerMinute`, `AiRequestsPerMinute`).

### Rejection response

When a client exceeds the rate limit, the middleware returns HTTP 429 with the [canonical error envelope](../api-reference/errors.md):

```json
{
  "code": "RATE_LIMITED",
  "message": "Rate limit exceeded",
  "request_id": "<correlation-id>"
}
```

Response headers: `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`.
