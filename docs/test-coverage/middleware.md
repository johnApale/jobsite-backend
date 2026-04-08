# Middleware Test Coverage

← [Test Coverage](README.md)

> Tests for the request pipeline middleware — error handling, correlation IDs, and tenant resolution.

---

## `AppErrorMiddlewareTests` (5 tests)

Tests the global exception handler that catches `AppError` exceptions and serializes them into the standard error envelope.

| Test                                                         | What It Verifies                                                      | Expected Outcome                                                             |
| ------------------------------------------------------------ | --------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| `InvokeAsync_AppErrorThrown_ReturnsCorrectStatusAndEnvelope` | `AppError` is caught and mapped to the error envelope JSON            | Status matches `AppError.StatusCode`, body has `code`/`message`/`request_id` |
| `InvokeAsync_AppErrorWithDetails_IncludesDetailsInEnvelope`  | Validation details dictionary is included in the envelope             | Body contains `details` with field-level errors                              |
| `InvokeAsync_UnhandledException_Returns500WithSafeMessage`   | Unexpected exceptions produce a generic 500 without leaking internals | Status 500, body has `INTERNAL_ERROR`, no exception message                  |
| `InvokeAsync_NoException_PassesThrough`                      | Successful requests pass through without modification                 | Next delegate is called, status remains 200                                  |
| `InvokeAsync_NoCorrelationId_FallsBackToTraceIdentifier`     | When no correlation ID is in Items, falls back to `TraceIdentifier`   | `request_id` in envelope matches `TraceIdentifier`                           |

**Why:** This middleware is the last line of defense for API error responses. If it fails to catch `AppError`, clients receive raw 500 errors with stack traces. If it leaks exception messages for unhandled errors, it creates a security vulnerability (information disclosure).

---

## `CorrelationIdMiddlewareTests` (4 tests)

Tests correlation ID propagation for distributed tracing across the monolith and AI Interview Service.

| Test                                                 | What It Verifies                                                   | Expected Outcome                                  |
| ---------------------------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------- |
| `InvokeAsync_RequestHasCorrelationId_UsesProvidedId` | Existing `X-Correlation-ID` header is preserved and forwarded      | Items["CorrelationId"] matches the provided value |
| `InvokeAsync_NoCorrelationIdHeader_GeneratesNewGuid` | Missing header triggers GUID generation                            | Items["CorrelationId"] is a valid GUID string     |
| `InvokeAsync_EchoesCorrelationIdOnResponse`          | Correlation ID is echoed back in the response header               | Response header `X-Correlation-ID` is present     |
| `InvokeAsync_StoresInHttpContextItems`               | Correlation ID is stored in `HttpContext.Items` for downstream use | Items["CorrelationId"] is not null                |

**Why:** Correlation IDs are essential for tracing requests across the monolith and the AI Interview microservice. If the middleware fails to generate or propagate them, distributed tracing breaks and debugging production issues across services becomes impossible.

---

## `TenantResolutionMiddlewareTests` (13 tests)

Tests the tenant resolution middleware that extracts the subdomain from the `Host` header, checks the tenant cache, looks up the tenant, and stores it in `HttpContext.Items`. Uses NSubstitute to mock `ITenantRepository` and `ITenantCache`.

| Test                                                       | What It Verifies                                                         | Expected Outcome                                                      |
| ---------------------------------------------------------- | ------------------------------------------------------------------------ | --------------------------------------------------------------------- |
| `InvokeAsync_HealthRoute_BypassesTenantResolution`         | `/health` is skipped — no tenant lookup                                  | Next called, repository not invoked                                   |
| `InvokeAsync_TenantsApiRoute_BypassesTenantResolution`     | `/api/v1/tenants/*` is skipped — tenant registration doesn't need tenant | Next called                                                           |
| `InvokeAsync_PlatformAdminRoute_BypassesTenantResolution`  | `/api/v1/platform/*` is skipped — platform admin operates on Catalog DB  | Next called, repository not invoked                                   |
| `InvokeAsync_OpenApiRoute_BypassesTenantResolution`        | `/openapi/*` is skipped — API docs accessible without tenant             | Next called                                                           |
| `InvokeAsync_LocalhostWithoutSubdomain_Returns400`         | `localhost` has no subdomain — returns 400 with `INVALID_REQUEST`        | Status 400, body contains `INVALID_REQUEST`                           |
| `InvokeAsync_ValidSubdomainNotFound_Returns404`            | Subdomain exists but tenant not in DB or cache — returns 404             | Status 404, body contains `TENANT_NOT_FOUND`                          |
| `InvokeAsync_SuspendedTenant_Returns403`                   | Suspended tenant returns 403 — blocks access                             | Status 403, body contains `FORBIDDEN` and `Suspended`                 |
| `InvokeAsync_ActiveTenant_StoresInContextAndCallsNext`     | Active tenant is stored in Items and pipeline continues                  | Items["Tenant"] set, Items["TenantConnectionString"] set, next called |
| `InvokeAsync_SubdomainWithPort_ExtractsCorrectly`          | Port numbers are stripped before subdomain extraction                    | Correct tenant resolved despite port in host header                   |
| `InvokeAsync_CachedTenant_SkipsRepository`                 | Cache hit skips the database lookup entirely                             | Repository not called, cache hit used                                 |
| `InvokeAsync_CacheMiss_QueriesRepositoryAndPopulatesCache` | Cache miss triggers DB query and populates the cache                     | Repository called, then `SetAsync` called on cache                    |
| `InvokeAsync_CorrelationIdInItems_IncludedInErrorResponse` | `request_id` in error JSON uses `HttpContext.Items["CorrelationId"]`     | Error response contains the correlation ID                            |
| `InvokeAsync_NoCorrelationId_UsesTraceIdentifier`          | When no correlation ID in Items, falls back to `TraceIdentifier`         | Error response contains `TraceIdentifier`                             |

**Why:** Tenant resolution runs on every request (except bypassed routes). If subdomain extraction fails, the wrong tenant's data is served — a catastrophic multi-tenancy violation. The bypass list prevents health checks and API docs from requiring a tenant, which would break monitoring and development. The suspended/403 check enforces billing and compliance controls.
