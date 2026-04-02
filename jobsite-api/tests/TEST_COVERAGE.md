# Test Coverage Working Document

> Living document tracking all implemented tests, their coverage, rationale, and expected outcomes.

## Test Summary

| Project                   | Tests   | Status                                               |
| ------------------------- | ------- | ---------------------------------------------------- |
| Jobsite.UnitTests         | 140     | ✅ All passing                                       |
| Jobsite.ArchitectureTests | 25      | ✅ All passing                                       |
| Jobsite.IntegrationTests  | 12 + 3  | ✅ All passing (3 provisioning tests require Docker) |
| **Total**                 | **180** |                                                      |

---

## Unit Tests (`Jobsite.UnitTests`)

### SharedKernel

#### `EntityTests` (2 tests)

Tests the base `Entity` class that every domain entity in every module inherits from. Defects here would cascade across the entire system.

| Test                                  | What It Verifies                                                         | Expected Outcome                     |
| ------------------------------------- | ------------------------------------------------------------------------ | ------------------------------------ |
| `Entity_NewInstance_HasDefaultGuidId` | A new entity starts with `Guid.Empty` before the database assigns a UUID | `Id` equals `Guid.Empty`             |
| `Entity_SetProperties_RetainsValues`  | `Id`, `CreatedAt`, and `UpdatedAt` can be set and read back correctly    | All properties match assigned values |

**Why:** Entity is the single inheritance root for all domain objects. If property assignment or defaults break, every module's persistence layer fails silently.

---

#### `AggregateRootTests` (4 tests)

Tests domain event tracking on `AggregateRoot`, which extends `Entity`. Aggregate roots collect domain events during business operations and dispatch them after `SaveChangesAsync` succeeds — this is the backbone of inter-module communication via MediatR.

| Test                                                 | What It Verifies                                                            | Expected Outcome                                      |
| ---------------------------------------------------- | --------------------------------------------------------------------------- | ----------------------------------------------------- |
| `RaiseDomainEvent_SingleEvent_AppearsInDomainEvents` | Calling `RaiseDomainEvent` adds the event to the collection                 | `DomainEvents` has exactly 1 item of the correct type |
| `RaiseDomainEvent_MultipleEvents_TracksAll`          | Multiple events are accumulated, not overwritten                            | `DomainEvents` has count 3 after 3 raises             |
| `ClearDomainEvents_AfterRaising_RemovesAll`          | `ClearDomainEvents()` empties the collection (called by UoW after dispatch) | `DomainEvents` is empty                               |
| `DomainEvents_NewAggregate_IsEmpty`                  | A freshly created aggregate has no pending events                           | `DomainEvents` is empty                               |

**Why:** Domain events are the only allowed communication channel between modules. If events are lost, duplicated, or not cleared after publishing, modules will either miss critical state changes (e.g., `ApplicationSubmittedEvent` never triggers screening) or process them repeatedly.

---

#### `ResultTests` (4 tests)

Tests the `Result<T>` monad used for operations that can fail without throwing exceptions. Provides railway-oriented error handling as an alternative to `AppError` exceptions.

| Test                                          | What It Verifies                                                   | Expected Outcome                                      |
| --------------------------------------------- | ------------------------------------------------------------------ | ----------------------------------------------------- |
| `Success_WithValue_IsSuccessTrue`             | `Result<T>.Success(value)` creates a successful result             | `IsSuccess` is true, `Value` matches, `Error` is null |
| `Failure_WithError_IsFailureTrue`             | `Result<T>.Failure(error)` creates a failed result                 | `IsFailure` is true, `Error.Code` matches             |
| `ImplicitConversion_FromValue_CreatesSuccess` | Assigning a raw value implicitly converts to `Result<T>.Success`   | `IsSuccess` is true, `Value` is 42                    |
| `ImplicitConversion_FromError_CreatesFailure` | Assigning an `AppError` implicitly converts to `Result<T>.Failure` | `IsFailure` is true, error code matches               |

**Why:** The implicit conversions enable clean return syntax (`return tenant;` instead of `return Result<Tenant>.Success(tenant);`). If implicit operators break, every service method using Result would need rewriting or would silently return wrong states.

---

#### `AppErrorTests` (6 tests)

Tests the `AppError` exception type and the `AppErrors` sentinel catalog. `AppError` is the canonical way to surface domain errors — caught by `AppErrorMiddleware` and serialized into the standard error envelope.

| Test                                                  | What It Verifies                                                                  | Expected Outcome                                            |
| ----------------------------------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| `AppError_Constructor_SetsProperties`                 | Constructor correctly sets `Code`, `StatusCode`, and `Message`                    | All properties match constructor args                       |
| `WithMessage_ReturnsNewInstanceWithCustomMessage`     | `WithMessage()` creates a new error with a custom message, preserving code/status | New instance with customized message, original unchanged    |
| `WithDetails_ReturnsNewInstanceWithValidationDetails` | `WithDetails()` attaches per-field validation errors                              | `Details` dictionary has expected entries                   |
| `AppErrors_TenantNotFound_Has404Status`               | Sentinel `TenantNotFound` has correct code and 404 status                         | Code is `TENANT_NOT_FOUND`, status is 404                   |
| `AppErrors_Unauthorized_Has401Status`                 | Sentinel `Unauthorized` has correct code and 401 status                           | Code is `UNAUTHORIZED`, status is 401                       |
| `AppErrors_SentinelProperties_ReturnNewInstances`     | Each access to a sentinel property returns a fresh instance                       | Two accesses to `TenantNotFound` are not the same reference |

**Why:** `AppErrors` sentinels are the single source of truth for all error codes and HTTP statuses across the platform. The "new instance each time" behavior is critical — if sentinels were shared instances, calling `.WithMessage()` in one request would mutate the error for all subsequent requests (thread-safety bug). The middleware relies on `Code` and `StatusCode` to build the API error envelope, so incorrect values would produce wrong HTTP responses.

---

#### `TenantDbContextTests` (5 tests)

Tests the abstract `TenantDbContext` base class that handles snake_case naming and domain event dispatch after save. Uses an in-memory EF Core provider with a concrete test subclass.

| Test                                                                | What It Verifies                                                                    | Expected Outcome                          |
| ------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | ----------------------------------------- |
| `SaveChangesAsync_AggregateWithEvents_DispatchesEventsAfterSave`    | Domain events from aggregate roots are dispatched via `IDomainEventDispatcher`      | Dispatcher receives each event            |
| `SaveChangesAsync_AggregateWithEvents_ClearsEventsAfterDispatch`    | Events are cleared from aggregate roots after dispatch to prevent duplicate publish | `DomainEvents` is empty after save        |
| `SaveChangesAsync_MultipleAggregatesWithEvents_DispatchesAllEvents` | Events from multiple aggregates in the same context are all dispatched              | All events from all aggregates dispatched |
| `SaveChangesAsync_NoEvents_DoesNotCallDispatcher`                   | Saving entities without events doesn't trigger the dispatcher                       | Dispatcher receives zero calls            |
| `SaveChangesAsync_NoDispatcher_SavesWithoutError`                   | Null dispatcher (optional dependency) doesn't cause errors                          | Save succeeds, no exception thrown        |

**Why:** `TenantDbContext` is the base for all module-level DbContexts. If domain event dispatch breaks here, no module can publish events after persistence. The "clear after dispatch" behavior prevents duplicate event processing. The null-dispatcher test ensures the base class works in test scenarios without DI.

---

### Tenancy Module

#### `TenantStatusTests` (7 tests)

Tests the `TenantStatus` constants and the `IsValid()` validation method. Status values must match the PostgreSQL CHECK constraint `chk_tenants_status` exactly.

| Test                                     | What It Verifies                                             | Expected Outcome |
| ---------------------------------------- | ------------------------------------------------------------ | ---------------- |
| `IsValid_Provisioning_ReturnsTrue`       | "Provisioning" is a valid status                             | Returns `true`   |
| `IsValid_Active_ReturnsTrue`             | "Active" is a valid status                                   | Returns `true`   |
| `IsValid_Suspended_ReturnsTrue`          | "Suspended" is a valid status                                | Returns `true`   |
| `IsValid_Deactivated_ReturnsTrue`        | "Deactivated" is a valid status                              | Returns `true`   |
| `IsValid_ProvisioningFailed_ReturnsTrue` | "ProvisioningFailed" is a valid status                       | Returns `true`   |
| `IsValid_UnknownStatus_ReturnsFalse`     | "Deleted" is rejected as an invalid status                   | Returns `false`  |
| `IsValid_LowercaseStatus_ReturnsFalse`   | "active" (lowercase) is rejected — PascalCase per convention | Returns `false`  |

**Why:** Status values are stored as strings and guarded by a database CHECK constraint. If the application writes a status the DB doesn't accept, the `INSERT`/`UPDATE` will throw a PostgreSQL constraint violation at runtime. These tests catch casing or spelling mismatches at build time instead of in production.

---

#### `TenantTests` (3 tests)

Tests the `Tenant` entity structure, its inheritance from `AggregateRoot`, and the `TenantBranding` navigation property.

| Test                                           | What It Verifies                                                      | Expected Outcome                                                                     |
| ---------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `Tenant_InheritsAggregateRoot_HasDomainEvents` | Tenant inherits domain event tracking from `AggregateRoot`            | `DomainEvents` collection exists and is empty                                        |
| `Tenant_NewInstance_HasExpectedDefaults`       | Factory-created tenant has correct property values and null optionals | Name, subdomain, status match; `ProvisionedAt`, `DeactivatedAt`, `Branding` are null |
| `Tenant_WithBranding_NavigationPropertySet`    | One-to-one branding association works correctly                       | `Branding` is not null, `TenantId` matches, colors populated                         |

**Why:** Tenant is the most accessed entity in the system — resolved on every inbound request via subdomain lookup. Validating its shape and relationships ensures the tenant resolution middleware, caching layer, and EF Core configuration will map correctly.

---

#### `TenantServiceTests` (10 tests)

Tests `TenantService`, the application service for tenant registration and lookup. Uses NSubstitute to mock `ITenantRepository`, `ITenantProvisioner`, and `IUnitOfWork`.

| Test                                                         | What It Verifies                                             | Expected Outcome                                                              |
| ------------------------------------------------------------ | ------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| `GetByIdAsync_ExistingTenant_ReturnsTenantResponse`          | Successful lookup maps entity to DTO correctly               | Response has matching `Id`, `Name`, `Subdomain`, `Status`                     |
| `GetByIdAsync_NonExistentId_ThrowsTenantNotFound`            | Missing tenant throws the correct `AppError`                 | Throws `AppError` with code `TENANT_NOT_FOUND`, status 404                    |
| `RegisterAsync_ValidRequest_CreatesProvisioningTenant`       | Happy path: tenant registered with "Provisioning" status     | Response matches request; `Add()` and `SaveChangesAsync()` each called once   |
| `RegisterAsync_DuplicateSubdomain_ThrowsInvalidRequest`      | Duplicate subdomain is rejected before persistence           | Throws `AppError` with code `INVALID_REQUEST`, message contains the subdomain |
| `RegisterAsync_DuplicateName_ThrowsInvalidRequest`           | Duplicate company name is rejected before persistence        | Throws `AppError` with code `INVALID_REQUEST`, message contains the name      |
| `RegisterAsync_ValidRequest_SubdomainIsLowercased`           | Subdomain "AcMe" is normalized to "acme"                     | Response subdomain is lowercase                                               |
| `GetByIdAsync_TenantWithBranding_IncludesBrandingInResponse` | Branding navigation property is mapped into the response DTO | `Branding` is not null, colors and tagline populated                          |
| `GetByIdAsync_TenantWithoutBranding_BrandingIsNull`          | Missing branding results in null, not an error               | `Branding` is null                                                            |
| `RegisterAsync_ValidRequest_TriggersProvisioning`            | Provisioner is called after tenant is saved                  | `ITenantProvisioner.ProvisionAsync()` received one call with tenant ID        |
| `RegisterAsync_ValidRequest_ProvisioningCalledAfterSave`     | Provisioning only runs after `SaveChangesAsync` succeeds     | Save is called before provision                                               |

**Why:** `TenantService` is the entry point for all tenant operations. The uniqueness checks (subdomain/name) prevent data integrity violations before they hit the database. Subdomain lowercasing is critical because DNS labels are case-insensitive — inconsistent casing would cause cache misses and lookup failures in the tenant resolution middleware. The mock-based approach validates business logic in isolation without needing a real database.

---

#### `MemoryTenantCacheTests` (5 tests)

Tests `MemoryTenantCache`, the `IMemoryCache`-backed tenant cache used by `TenantResolutionMiddleware` for cache-first tenant lookups.

| Test                                             | What It Verifies                                                  | Expected Outcome                  |
| ------------------------------------------------ | ----------------------------------------------------------------- | --------------------------------- |
| `GetBySubdomainAsync_CachedTenant_ReturnsTenant` | Cached tenant is returned on subsequent lookups                   | Returns the same tenant entity    |
| `GetBySubdomainAsync_NotCached_ReturnsNull`      | Cache miss returns null (triggers DB fallback in middleware)      | Returns `null`                    |
| `SetAsync_StoresTenantInCache`                   | `SetAsync` makes the tenant retrievable via `GetBySubdomainAsync` | Subsequent get returns the tenant |
| `InvalidateAsync_RemovesTenantFromCache`         | `InvalidateAsync` removes the cached entry                        | Subsequent get returns `null`     |
| `InvalidateAsync_NonExistentKey_DoesNotThrow`    | Invalidating a key that doesn't exist is a no-op                  | No exception thrown               |

**Why:** The tenant cache sits on the hot path of every request. A broken cache would either serve stale tenant data (security risk — suspended tenant still served) or fall back to DB on every request (performance degradation). The invalidation tests ensure tenant status changes (e.g., suspension) take effect promptly.

---

### Middleware

#### `AppErrorMiddlewareTests` (5 tests)

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

#### `CorrelationIdMiddlewareTests` (4 tests)

Tests correlation ID propagation for distributed tracing across the monolith and AI Interview Service.

| Test                                                 | What It Verifies                                                   | Expected Outcome                                  |
| ---------------------------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------- |
| `InvokeAsync_RequestHasCorrelationId_UsesProvidedId` | Existing `X-Correlation-ID` header is preserved and forwarded      | Items["CorrelationId"] matches the provided value |
| `InvokeAsync_NoCorrelationIdHeader_GeneratesNewGuid` | Missing header triggers GUID generation                            | Items["CorrelationId"] is a valid GUID string     |
| `InvokeAsync_EchoesCorrelationIdOnResponse`          | Correlation ID is echoed back in the response header               | Response header `X-Correlation-ID` is present     |
| `InvokeAsync_StoresInHttpContextItems`               | Correlation ID is stored in `HttpContext.Items` for downstream use | Items["CorrelationId"] is not null                |

**Why:** Correlation IDs are essential for tracing requests across the monolith and the AI Interview microservice. If the middleware fails to generate or propagate them, distributed tracing breaks and debugging production issues across services becomes impossible.

---

#### `TenantResolutionMiddlewareTests` (12 tests)

Tests the tenant resolution middleware that extracts the subdomain from the `Host` header, checks the tenant cache, looks up the tenant, and stores it in `HttpContext.Items`. Uses NSubstitute to mock `ITenantRepository` and `ITenantCache`.

| Test                                                       | What It Verifies                                                         | Expected Outcome                                                      |
| ---------------------------------------------------------- | ------------------------------------------------------------------------ | --------------------------------------------------------------------- |
| `InvokeAsync_HealthRoute_BypassesTenantResolution`         | `/health` is skipped — no tenant lookup                                  | Next called, repository not invoked                                   |
| `InvokeAsync_TenantsApiRoute_BypassesTenantResolution`     | `/api/v1/tenants/*` is skipped — tenant registration doesn't need tenant | Next called                                                           |
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

---

#### `IntegrationEventSerializationTests` (6 tests)

Verifies that integration events — which cross the C# → Python boundary via the message broker — serialize to snake_case JSON and round-trip without data loss. The AI Interview Service (Python/FastAPI) deserializes these events using Pydantic, so the JSON contract must remain stable.

| Test                                                        | What It Verifies                                                            | Expected Outcome                                        |
| ----------------------------------------------------------- | --------------------------------------------------------------------------- | ------------------------------------------------------- |
| `CandidateReadyForInterviewEvent_SerializesToSnakeCaseJson` | All property names serialize to snake_case (`event_id`, `tenant_id`, etc.)  | JSON contains all expected snake_case keys              |
| `CandidateReadyForInterviewEvent_RoundTripsWithoutDataLoss` | Serialize → deserialize produces identical property values                  | All fields match original                               |
| `CandidateReadyForInterviewEvent_NoPascalCaseKeysInOutput`  | PascalCase property names (`EventId`, `TenantId`) do NOT appear in JSON     | No PascalCase keys found in serialized output           |
| `InterviewCompletedEvent_SerializesToSnakeCaseJson`         | All property names serialize to snake_case including `interview_session_id` | JSON contains all expected snake_case keys              |
| `InterviewCompletedEvent_RoundTripsWithoutDataLoss`         | Serialize → deserialize preserves `OverallScore` and all GUIDs              | All fields match original                               |
| `InterviewCompletedEvent_OverallScoreSerializesAsNumber`    | `OverallScore` (int) serializes as a JSON number, not a quoted string       | JSON contains `"overall_score":75` (no quotes on value) |

**Why:** These are the contract tests for the most critical cross-service boundary in the architecture. The .NET monolith publishes `CandidateReadyForInterviewEvent` to RabbitMQ/Azure Service Bus, and the Python AI Interview Service consumes it. If the JSON shape changes (e.g., `eventId` instead of `event_id`), the Python service will silently drop fields or crash. The `NoPascalCaseKeys` test is a safety net against accidentally using the wrong `JsonSerializerOptions`.

---

### Pipeline Behaviors

#### `LoggingPipelineBehaviorTests` (3 tests)

Tests the MediatR pipeline behavior that logs request start/finish with elapsed time.

| Test                                        | What It Verifies                                                | Expected Outcome                          |
| ------------------------------------------- | --------------------------------------------------------------- | ----------------------------------------- |
| `Handle_LogsStartAndCompletion`             | Logs "Handling {RequestName}..." and "Handled {RequestName} in" | Logger receives both log entries          |
| `Handle_ReturnsHandlerResult`               | The behavior passes through the handler's return value          | Response matches what the handler returns |
| `Handle_WhenNextThrows_PropagatesException` | Exceptions from the handler are not swallowed                   | Exception propagates to caller            |

**Why:** The logging behavior wraps every MediatR request. If it swallows exceptions or fails to pass through results, every command and query in the system breaks silently.

---

#### `ValidationPipelineBehaviorTests` (4 tests)

Tests the MediatR pipeline behavior that runs FluentValidation validators before the handler.

| Test                                                  | What It Verifies                                                           | Expected Outcome                                             |
| ----------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `Handle_NoValidators_PassesThrough`                   | When no `IValidator<TRequest>` is registered, the handler executes         | Handler result returned                                      |
| `Handle_ValidRequest_PassesThrough`                   | When validators pass, the handler executes                                 | Handler result returned                                      |
| `Handle_ValidationFails_ThrowsValidationError`        | When any validator fails, throws `AppErrors.Validation` with field details | Throws `AppError` with code `VALIDATION_ERROR` and `Details` |
| `Handle_MultipleValidationFailures_AggregatesDetails` | Multiple validator failures are aggregated into one `Details` dictionary   | All failing fields present in `Details`                      |

**Why:** Validation runs before every handler. If it fails to throw on invalid input, business logic processes garbage data. If it throws on valid input, every request fails. The aggregation test ensures multiple validation rules don't lose errors.

---

### Infrastructure

#### `MassTransitEventPublisherTests` (2 tests)

Tests `MassTransitEventPublisher`, the `IEventPublisher` implementation that wraps MassTransit's `IPublishEndpoint`.

| Test                                     | What It Verifies                                                  | Expected Outcome                               |
| ---------------------------------------- | ----------------------------------------------------------------- | ---------------------------------------------- |
| `PublishAsync_CallsPublishEndpoint`      | The publisher delegates to `IPublishEndpoint.Publish()` correctly | `IPublishEndpoint.Publish()` called with event |
| `PublishAsync_ForwardsCancellationToken` | The `CancellationToken` is forwarded to MassTransit               | Token passed through to `Publish()`            |

**Why:** `MassTransitEventPublisher` is the single exit point for all integration events leaving the monolith. If it fails to forward events to the broker, the AI Interview Service never receives candidate readiness notifications.

---

### Auth Module

#### `UserTests` (4 tests)

Tests the `User` aggregate root entity structure, domain event raising, and default collection initialization.

| Test                                         | What It Verifies                                            | Expected Outcome                         |
| -------------------------------------------- | ----------------------------------------------------------- | ---------------------------------------- |
| `CreateUser_WithValidData_SetsProperties`    | User creation with valid data sets all properties correctly | Properties match provided values         |
| `Raise_DomainEvent_IsCapturedInDomainEvents` | `Raise()` method adds domain events to the collection       | `DomainEvents` contains the raised event |
| `ExternalLogins_DefaultsToEmptyList`         | New user starts with no external logins                     | `ExternalLogins` is empty                |
| `RefreshTokens_DefaultsToEmptyList`          | New user starts with no refresh tokens                      | `RefreshTokens` is empty                 |

**Why:** The `User` aggregate root is the central entity of the Auth module. The `Raise()` method wrapper is essential because `RaiseDomainEvent` is protected on `AggregateRoot` — if this delegation breaks, `UserRegisteredEvent` will never fire.

---

#### `RefreshTokenTests` (4 tests)

Tests the `RefreshToken` entity behavior, specifically the revocation and expiration logic.

| Test                                           | What It Verifies                                         | Expected Outcome                       |
| ---------------------------------------------- | -------------------------------------------------------- | -------------------------------------- |
| `Revoke_SetsIsRevokedAndRevokedAt`             | `Revoke()` sets `IsRevoked = true` and records timestamp | Both properties updated correctly      |
| `IsExpired_WhenExpiresAtInPast_ReturnsTrue`    | Expired tokens are detected correctly                    | Returns `true`                         |
| `IsExpired_WhenExpiresAtInFuture_ReturnsFalse` | Non-expired tokens are detected correctly                | Returns `false`                        |
| `CreateRefreshToken_DefaultsIsRevokedToFalse`  | New tokens start as not revoked                          | `IsRevoked` is false, `RevokedAt` null |

**Why:** Refresh token revocation and expiration are security-critical. If `Revoke()` doesn't set both flags, token replay detection breaks. If `IsExpired` calculations are wrong, expired tokens could be accepted.

---

#### `PasswordHasherTests` (5 tests)

Tests the BCrypt password hashing implementation.

| Test                                                       | What It Verifies                                       | Expected Outcome             |
| ---------------------------------------------------------- | ------------------------------------------------------ | ---------------------------- |
| `HashPassword_ReturnsNonEmptyHash`                         | BCrypt produces a valid hash starting with `$2a$`      | Non-empty BCrypt hash string |
| `HashPassword_DifferentInputs_ProduceDifferentHashes`      | Different passwords produce different hashes           | Hashes are different         |
| `HashPassword_SameInput_ProducesDifferentHashes_DueToSalt` | Same password produces unique hashes (salt uniqueness) | Hashes are different         |
| `VerifyPassword_CorrectPassword_ReturnsTrue`               | Correct password verifies against its hash             | Returns `true`               |
| `VerifyPassword_WrongPassword_ReturnsFalse`                | Wrong password fails verification                      | Returns `false`              |

**Why:** Password hashing is the most security-critical code in the Auth module. Failures here could allow password bypass (if verify always returns true) or lock out all users (if hashing is broken).

---

#### `JwtServiceTests` (9 tests)

Tests JWT access token generation, refresh token generation, and SHA-256 token hashing.

| Test                                                  | What It Verifies                                            | Expected Outcome               |
| ----------------------------------------------------- | ----------------------------------------------------------- | ------------------------------ |
| `GenerateAccessToken_ReturnsValidJwt`                 | Generated token is a valid JWT with correct issuer/audience | Valid JWT structure            |
| `GenerateAccessToken_ContainsExpectedClaims`          | Token contains sub, email, role, tenant_id claims           | All claims present with values |
| `GenerateAccessToken_ExpiresInConfiguredMinutes`      | Token expiration matches configured value                   | Expires ~30 minutes from now   |
| `GenerateRefreshToken_ReturnsNonEmptyBase64String`    | Refresh token is 64-byte base64-encoded string              | Valid base64, 64 bytes         |
| `GenerateRefreshToken_ProducesUniqueTokens`           | Each generated token is unique                              | Two tokens are different       |
| `HashToken_SameInput_ProducesSameHash`                | SHA-256 hash is deterministic                               | Same hash for same input       |
| `HashToken_DifferentInputs_ProduceDifferentHashes`    | Different inputs produce different hashes                   | Different hashes               |
| `AccessTokenExpirationMinutes_ReturnsConfiguredValue` | Configuration value is exposed correctly                    | Returns 30                     |
| `RefreshTokenExpirationDays_ReturnsConfiguredValue`   | Configuration value is exposed correctly                    | Returns 7                      |

**Why:** JWT tokens are the primary authentication mechanism. Incorrect claims would break authorization policies. Non-deterministic token hashing would break refresh token lookup. Predictable refresh tokens would be a security vulnerability.

---

#### `AuthServiceTests` (18 tests)

Tests the `AuthService` application service with mocked dependencies. Covers register, login, refresh, OAuth, logout, and get-current-user flows.

| Test                                                         | What It Verifies                                         | Expected Outcome                           |
| ------------------------------------------------------------ | -------------------------------------------------------- | ------------------------------------------ |
| `RegisterAsync_ValidRequest_ReturnsTokens`                   | Successful registration issues access and refresh tokens | Tokens returned, user added                |
| `RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailError`     | Duplicate email throws `DUPLICATE_EMAIL`                 | `AppError` with code `DUPLICATE_EMAIL`     |
| `RegisterAsync_NoRoleProvided_DefaultsToApplicant`           | Missing role defaults to `Applicant`                     | User created with `Applicant` role         |
| `RegisterAsync_RaisesUserRegisteredEvent`                    | Registration raises `UserRegisteredEvent`                | User has 1 domain event                    |
| `LoginAsync_ValidCredentials_ReturnsTokens`                  | Valid email/password returns tokens                      | Tokens returned                            |
| `LoginAsync_UserNotFound_ThrowsInvalidCredentials`           | Non-existent email throws `INVALID_CREDENTIALS`          | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_DeactivatedUser_ThrowsInvalidCredentials`        | Deactivated user cannot login                            | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_NullPasswordHash_ThrowsInvalidCredentials`       | OAuth-only user cannot login with password               | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_WrongPassword_ThrowsInvalidCredentials`          | Wrong password throws error                              | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_ValidCredentials_UpdatesLastLoginAt`             | Successful login updates `LastLoginAt`                   | Timestamp set to now                       |
| `RefreshTokenAsync_ValidToken_ReturnsNewTokens`              | Valid refresh rotates and returns new tokens             | New tokens, old token revoked              |
| `RefreshTokenAsync_TokenNotFound_ThrowsInvalidCredentials`   | Unknown token throws error                               | `AppError` with code `INVALID_CREDENTIALS` |
| `RefreshTokenAsync_RevokedToken_RevokesEntireFamily`         | Reused revoked token triggers family-wide revocation     | `TOKEN_REPLAY_DETECTED`, family revoked    |
| `RefreshTokenAsync_ExpiredToken_ThrowsTokenExpired`          | Expired token throws `TOKEN_EXPIRED`                     | `AppError` with code `TOKEN_EXPIRED`       |
| `RefreshTokenAsync_DeactivatedUser_ThrowsInvalidCredentials` | Deactivated user cannot refresh                          | `AppError` with code `INVALID_CREDENTIALS` |
| `LogoutAsync_ValidToken_RevokesIt`                           | Logout revokes the refresh token                         | Token revoked, changes saved               |
| `LogoutAsync_TokenNotFound_DoesNotThrow`                     | Logout with invalid token is idempotent                  | No exception thrown                        |
| `LogoutAsync_AlreadyRevokedToken_DoesNotSaveAgain`           | Already-revoked token doesn't trigger save               | `SaveChangesAsync` not called              |
| `GetCurrentUserAsync_ExistingUser_ReturnsUserResponse`       | Returns user profile for valid user ID                   | UserResponse with correct data             |
| `GetCurrentUserAsync_UserNotFound_ThrowsUserNotFound`        | Non-existent user throws `USER_NOT_FOUND`                | `AppError` with code `USER_NOT_FOUND`      |
| `OAuthLoginAsync_InvalidProvider_ThrowsInvalidRequest`       | Invalid provider name throws error                       | `AppError` with code `INVALID_REQUEST`     |
| `OAuthLoginAsync_ExistingLinkedAccount_ReturnsTokens`        | User with linked OAuth account gets tokens               | Tokens returned, `LastLoginAt` updated     |
| `OAuthLoginAsync_NewUser_CreatesUserAndReturnsTokens`        | New OAuth user is created with linked provider           | User created with external login           |

**Why:** `AuthService` contains all authentication business logic. These tests verify every authentication path including edge cases like deactivated users, OAuth-only users, token replay detection, and idempotent logout.

---

#### `RegisterRequestValidatorTests` (7 tests)

Tests FluentValidation rules for the registration request.

| Test                                        | What It Verifies                                    | Expected Outcome    |
| ------------------------------------------- | --------------------------------------------------- | ------------------- |
| `Validate_ValidRequest_IsValid`             | Valid request passes all rules                      | `IsValid` is true   |
| `Validate_EmptyEmail_HasValidationError`    | Empty email is rejected                             | Error on `Email`    |
| `Validate_InvalidEmail_HasValidationError`  | Malformed email is rejected                         | Error on `Email`    |
| `Validate_ShortPassword_HasValidationError` | Password under 8 chars is rejected                  | Error on `Password` |
| `Validate_ValidRole_IsValid`                | Valid role passes validation                        | `IsValid` is true   |
| `Validate_InvalidRole_HasValidationError`   | Invalid role string is rejected                     | Error on `Role`     |
| `Validate_NullRole_IsValid`                 | Null role is accepted (defaults to Applicant later) | `IsValid` is true   |

**Why:** Input validation is the first line of defense. Malformed requests should be caught before reaching the service layer.

---

#### `AuthConstantsTests` (6 tests)

Tests domain constant validation methods for `UserRole`, `UserStatus`, and `ExternalLoginProvider`.

| Test                                                         | What It Verifies                         | Expected Outcome |
| ------------------------------------------------------------ | ---------------------------------------- | ---------------- |
| `UserRole_IsValid_ValidRole_ReturnsTrue`                     | All 5 valid roles pass validation        | Returns `true`   |
| `UserRole_IsValid_InvalidRole_ReturnsFalse`                  | Invalid/lowercase roles are rejected     | Returns `false`  |
| `UserStatus_IsValid_ValidStatus_ReturnsTrue`                 | All 3 valid statuses pass validation     | Returns `true`   |
| `UserStatus_IsValid_InvalidStatus_ReturnsFalse`              | Invalid statuses are rejected            | Returns `false`  |
| `ExternalLoginProvider_IsValid_ValidProvider_ReturnsTrue`    | All 3 valid providers pass validation    | Returns `true`   |
| `ExternalLoginProvider_IsValid_InvalidProvider_ReturnsFalse` | Invalid/lowercase providers are rejected | Returns `false`  |

**Why:** These constants must match PostgreSQL CHECK constraints exactly. Case mismatches would cause runtime database errors.

---

## Architecture Tests (`Jobsite.ArchitectureTests`)

Architecture tests enforce structural rules at build time using NetArchTest. They prevent architectural drift as the codebase grows.

### `LayerDependencyTests` (5 tests)

Enforces the module layer dependency direction: `Domain → SharedKernel only`, `Application → Domain only`.

| Test                                                      | What It Verifies                                                         | Expected Outcome         |
| --------------------------------------------------------- | ------------------------------------------------------------------------ | ------------------------ |
| `DomainLayer_ShouldNotReference_ApplicationLayer`         | Tenancy.Domain has no dependency on Tenancy.Application                  | No violating types found |
| `DomainLayer_ShouldNotReference_InfrastructureLayer`      | Tenancy.Domain has no dependency on Tenancy.Infrastructure               | No violating types found |
| `DomainLayer_ShouldNotReference_EFCore`                   | Tenancy.Domain has no dependency on `Microsoft.EntityFrameworkCore`      | No violating types found |
| `ApplicationLayer_ShouldNotReference_InfrastructureLayer` | Tenancy.Application has no dependency on Tenancy.Infrastructure          | No violating types found |
| `ApplicationLayer_ShouldNotReference_EFCore`              | Tenancy.Application has no dependency on `Microsoft.EntityFrameworkCore` | No violating types found |

**Why:** The modular monolith architecture requires strict dependency direction to keep modules independently testable and refactorable. If the domain layer references EF Core, it becomes impossible to test business logic without a database. If application references infrastructure, swapping implementations (e.g., switching from PostgreSQL to another store) requires touching business logic. These tests catch accidental `using` statements added by IDE auto-imports.

---

### `NamingConventionTests` (4 tests)

Enforces coding standards from `docs/conventions/DOTNET_CONVENTIONS.md` across all modules and SharedKernel.

| Test                                                                                    | What It Verifies                                                                    | Expected Outcome         |
| --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------ |
| `ConcreteClasses_ShouldBeSealed_InDomain(module)` [Theory × all modules + SharedKernel] | All concrete classes in Domain layers are `sealed` (excluding EF migration classes) | No violating types found |
| `ConcreteClasses_ShouldBeSealed_InInfrastructure(module)` [Theory × all modules]        | All concrete classes in Infrastructure layers are `sealed`                          | No violating types found |
| `Interfaces_ShouldStartWithI_InDomain(module)` [Theory × all modules + SharedKernel]    | All interfaces follow the `I` prefix convention                                     | No violating types found |
| `Interfaces_ShouldStartWithI_InInfrastructure(module)` [Theory × all modules]           | All interfaces in Infrastructure follow the `I` prefix convention                   | No violating types found |

**Why:** The project mandate is `sealed class` on all concrete classes unless inheritance is explicitly needed. Applies across all 8 modules and SharedKernel, not just Tenancy. EF Core migration classes are excluded because they're auto-generated and inherit from `Migration`.

---

### `ModuleIsolationTests` (16 tests)

Enforces that modules do not cross-reference each other — modules communicate only through SharedKernel domain events. Tests all 8 modules via `[Theory]` with `[MemberData]`.

| Test                                                                | What It Verifies                                                               | Expected Outcome         |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------ | ------------------------ |
| `DomainLayer_ShouldNotReference_OtherModules(module)` [× 8]         | Each module's Domain has no dependency on any other module's namespace         | No violating types found |
| `InfrastructureLayer_ShouldNotReference_OtherModules(module)` [× 8] | Each module's Infrastructure has no dependency on any other module's namespace | No violating types found |

Modules tested: Tenancy, Auth, Admin, Profiles, Recruitment, Screening, Matching, HRWorkflows.

**Why:** Cross-module references are the primary way a modular monolith degrades into a big ball of mud. If Tenancy references Recruitment directly, extracting either into a separate service later becomes impossible. These tests enforce that inter-module communication goes through SharedKernel events only, keeping module boundaries clean.

---

## Integration Tests (`Jobsite.IntegrationTests`)

**Infrastructure:** Real PostgreSQL 16 via Testcontainers. EF Core migrations applied to the container. Data isolation via `TRUNCATE CASCADE` between tests.

### Fixture

- `CatalogIntegrationFixture` — spins up a `postgres:16-alpine` container, creates `CatalogDbContext`, runs migrations. Exposes `ConnectionString` property for provisioning tests that need direct database access.
- `CatalogIntegrationCollection` — xUnit `[Collection("Catalog")]` for shared container across test classes
- `IntegrationTestData` — factory with unique-per-test names/subdomains to avoid collisions

### `TenantRepositoryTests` (12 tests)

Tests `TenantRepository` against a real PostgreSQL database. Validates that EF Core configurations, snake_case column mapping, CHECK constraints, and unique indexes work correctly end-to-end.

| Test                                                           | What It Verifies                                                                      | Expected Outcome                                                           |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `Add_ValidTenant_PersistsToDatabase`                           | `Add()` + `SaveChangesAsync()` inserts a row with DB-assigned UUID and timestamps     | Re-queried tenant has non-empty `Id`, `CreatedAt`/`UpdatedAt` close to now |
| `GetBySubdomainAsync_ExistingTenant_ReturnsTenantWithBranding` | Subdomain lookup eager-loads the branding navigation property                         | Tenant returned with non-null `Branding`, correct `PrimaryColor`           |
| `GetBySubdomainAsync_NonExistent_ReturnsNull`                  | Missing subdomain returns null, not an exception                                      | Returns `null`                                                             |
| `GetByIdAsync_ExistingTenant_ReturnsTenant`                    | ID-based lookup returns the correct tenant                                            | Name matches                                                               |
| `GetByIdAsync_NonExistentId_ReturnsNull`                       | Random GUID returns null                                                              | Returns `null`                                                             |
| `SubdomainExistsAsync_ExistingSubdomain_ReturnsTrue`           | Existence check for a persisted subdomain                                             | Returns `true`                                                             |
| `SubdomainExistsAsync_NonExistent_ReturnsFalse`                | Existence check for a missing subdomain                                               | Returns `false`                                                            |
| `NameExistsAsync_ExistingName_ReturnsTrue`                     | Existence check for a persisted company name                                          | Returns `true`                                                             |
| `NameExistsAsync_NonExistent_ReturnsFalse`                     | Existence check for a missing company name                                            | Returns `false`                                                            |
| `Add_DuplicateSubdomain_ThrowsDbUpdateException`               | Unique index `ix_tenants_subdomain` rejects duplicate subdomains                      | Throws `DbUpdateException`                                                 |
| `Add_DuplicateName_ThrowsDbUpdateException`                    | Unique index `ix_tenants_name` rejects duplicate company names                        | Throws `DbUpdateException`                                                 |
| `Add_InvalidStatus_ThrowsDbUpdateException`                    | CHECK constraint `chk_tenants_status` rejects invalid status values (e.g., "Deleted") | Throws `DbUpdateException`                                                 |

**Why:** Unit tests with mocked repositories can't catch EF Core configuration bugs — wrong column types, missing indexes, misconfigured relationships, or CHECK constraint mismatches only surface against a real database. These tests prove that the migration, entity configurations, and repository queries actually work against PostgreSQL. The constraint tests (`DuplicateSubdomain`, `DuplicateName`, `InvalidStatus`) are particularly important because they validate that the database enforces invariants the application layer assumes.

**Prerequisite:** Docker must be running. Testcontainers automatically pulls and manages the PostgreSQL container.

---

### `TenantProvisioningTests` (3 tests)

Tests `TenantProvisioner` against a real PostgreSQL container. Validates that tenant database provisioning creates actual databases with correct connection strings and status transitions.

| Test                                                      | What It Verifies                                                                          | Expected Outcome                                                                                 |
| --------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `ProvisionAsync_ValidTenant_CreatesDatabaseAndSetsActive` | Provisioning creates a real PostgreSQL database and transitions tenant to `Active` status | Database exists, status is `Active`, `ConnectionString` contains DB name, `ProvisionedAt` is set |
| `ProvisionAsync_TwoTenants_GetDistinctDatabases`          | Two provisioned tenants get separate databases with distinct connection strings           | Each tenant has its own database and connection string; both databases exist                     |
| `ProvisionAsync_NonExistentTenant_ThrowsTenantNotFound`   | Provisioning a non-existent tenant ID throws an error                                     | Throws exception                                                                                 |

**Why:** Tenant database isolation is a security-critical feature. These tests verify that `CREATE DATABASE` actually executes against PostgreSQL, that connection strings resolve correctly, and that two tenants never share a database. The distinct-databases test is the primary guard against cross-tenant data leakage.

**Cleanup:** Tests clean up created databases (`DROP DATABASE`) in `DisposeAsync` to avoid polluting the test container.

---

## Test Data Factories

### `TestData.cs` (Unit Tests)

Centralized factory methods for unit test object creation. Avoids inline object construction per `docs/conventions/TESTING_STANDARDS.md`.

| Method                          | Creates                     | Default Values                                       |
| ------------------------------- | --------------------------- | ---------------------------------------------------- |
| `CreateTenant()`                | `Tenant` entity             | Name: "Acme Corp", Subdomain: "acme", Status: Active |
| `CreateTenantBranding()`        | `TenantBranding` entity     | Primary: #1A73E8, Tagline: "Test tagline"            |
| `CreateRegisterTenantRequest()` | `RegisterTenantRequest` DTO | Name: "Acme Corp", Subdomain: "acme"                 |

All methods accept optional overrides for customization in specific test scenarios.

### `IntegrationTestData.cs` (Integration Tests)

Factory methods generating unique names/subdomains per test to avoid collisions in the shared database.

| Method             | Creates                 | Default Values                                         |
| ------------------ | ----------------------- | ------------------------------------------------------ |
| `CreateTenant()`   | `Tenant` entity         | Unique name/subdomain via `Guid`, Status: Active       |
| `CreateBranding()` | `TenantBranding` entity | Primary: #1A73E8, Tagline: "Integration test branding" |

---

## Coverage Gaps & Next Steps

| Area                         | Gap                                                                                                   | Priority    |
| ---------------------------- | ----------------------------------------------------------------------------------------------------- | ----------- |
| **Endpoint Tests**           | No `WebApplicationFactory` tests for `TenantEndpoints` (POST/GET) — best added after Auth is wired up | High        |
| **Auth Module**              | Not yet implemented — will need JWT issuance, refresh token, replay detection tests                   | Next module |
| **Tenant Isolation Depth**   | No cross-tenant data visibility tests (write via tenant A, query via tenant B → zero results)         | High        |
| **MassTransit Integration**  | No end-to-end test with Testcontainers RabbitMQ — requires Testcontainers.RabbitMq package            | Medium      |
| **TenantDbContextFactory**   | Factory not yet implemented — needed before module-level DbContext integration tests                  | Medium      |
| **RequestLoggingMiddleware** | Not directly tested — logs via Serilog, lower value without log sink assertions                       | Low         |
