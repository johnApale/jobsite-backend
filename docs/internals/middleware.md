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
  │ 7. UseSerilogRequestLogging()   │ ← Serilog enriched request log
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
Host header → extract subdomain → look up tenant → validate status → store in HttpContext
```

1. **Extract subdomain** from `Host` header (e.g. `acme.djobsite.com` → `acme`).
2. **Skip** if the route is a non-tenant route (see bypass list below).
3. **Look up** the tenant by subdomain via `ITenantRepository.GetBySubdomainAsync()`.
4. **Validate** the tenant's status is `Active`. Reject `Suspended` / `Deactivated` tenants with 403.
5. **Store** in `HttpContext.Items`:
   - `"Tenant"` → the `Tenant` entity
   - `"TenantConnectionString"` → the tenant's database connection string

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
