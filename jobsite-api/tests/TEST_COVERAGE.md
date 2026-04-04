# Test Coverage Working Document

> Living document tracking all implemented tests, their coverage, rationale, and expected outcomes.

## Test Summary

| Project                   | Tests   | Status                                    |
| ------------------------- | ------- | ----------------------------------------- |
| Jobsite.UnitTests         | 349     | ✅ All passing                            |
| Jobsite.ArchitectureTests | 30      | ✅ All passing                            |
| Jobsite.IntegrationTests  | 58      | ✅ All passing (all tests require Docker) |
| **Total**                 | **437** |                                           |

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

### Admin Module

#### `AdminSettingsServiceTests` (8 tests)

Tests `AdminSettingsService` JSON merge-patch logic, JSONB serialization, and error handling for company settings management.

| Test                                                    | What It Verifies                                                   | Expected Outcome                             |
| ------------------------------------------------------- | ------------------------------------------------------------------ | -------------------------------------------- |
| `GetSettingsAsync_ExistingSettings_ReturnsResponse`     | Retrieves persisted settings and maps to response DTO              | All 8 settings blocks returned correctly     |
| `GetSettingsAsync_NoSettings_ThrowsSettingsNotFound`    | Missing settings throws `AppError` with `SETTINGS_NOT_FOUND`       | Throws `AppError` (404)                      |
| `UpdateSettingsAsync_AuthSettings_MergesCorrectly`      | Only provided `AuthSettings` fields are applied (JSON merge patch) | Auth updated, other blocks unchanged         |
| `UpdateSettingsAsync_MultipleBlocks_MergesAll`          | Multiple settings blocks updated in one request                    | All provided blocks merged, others untouched |
| `UpdateSettingsAsync_NullFields_LeavesUnchanged`        | Null fields in request are skipped (not overwritten)               | Original values preserved                    |
| `UpdateSettingsAsync_Timezone_UpdatesScalar`            | `DefaultTimezone` scalar field updates correctly                   | Timezone changed                             |
| `UpdateSettingsAsync_Currency_UpdatesScalar`            | `DefaultCurrency` scalar field updates correctly                   | Currency changed                             |
| `UpdateSettingsAsync_NoSettings_ThrowsSettingsNotFound` | Updating non-existent settings throws error                        | Throws `AppError` (404)                      |

**Why:** The JSON merge-patch strategy (only non-null fields overwrite) is the core Admin endpoint behavior. If merge logic is wrong, partial updates could silently wipe unrelated settings blocks.

---

#### `AuditLogServiceTests` (5 tests)

Tests `AuditLogService` creation, JSON detail serialization, and query delegation.

| Test                                          | What It Verifies                                            | Expected Outcome                      |
| --------------------------------------------- | ----------------------------------------------------------- | ------------------------------------- |
| `LogAsync_ValidInput_CreatesAuditLogAndSaves` | Creates `AuditLog` entity with correct fields and saves     | Entity added, UoW saved               |
| `LogAsync_WithDetails_SerializesAsJson`       | Detail object is serialized to JSON string                  | Details contain JSON representation   |
| `LogAsync_NullDetails_SetsDetailsNull`        | Null details are stored as null, not empty string           | `Details` is null                     |
| `LogAsync_NullEntityId_AllowsNullEntityId`    | Entity-less audit events (e.g., login) have null `EntityId` | `EntityId` is null                    |
| `QueryAsync_DelegatesToRepository`            | Query parameters are forwarded to `IAuditLogRepository`     | Repository called with correct params |

**Why:** Audit log creation is triggered by every domain event handler. If serialization or null handling breaks, audit trail integrity is compromised across all modules.

---

#### `AuditEventHandlerTests` (6 tests)

Tests the 6 domain event audit handlers that convert SharedKernel events into audit log entries.

| Test                                                    | What It Verifies                                                          | Expected Outcome                                          |
| ------------------------------------------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------- |
| `UserRegisteredAuditHandler_LogsCorrectAction`          | `UserRegisteredEvent` creates audit log with `UserRegistered` action      | `LogAsync` called with `AuditAction.UserRegistered`       |
| `ApplicationSubmittedAuditHandler_LogsCorrectAction`    | `ApplicationSubmittedEvent` creates audit log with `ApplicationSubmitted` | `LogAsync` called with `AuditAction.ApplicationSubmitted` |
| `CvScreeningCompletedAuditHandler_LogsCorrectAction`    | `CvScreeningCompletedEvent` creates audit log with `CvScreeningCompleted` | `LogAsync` called with correct action and entity type     |
| `CandidateShortlistedAuditHandler_LogsCorrectAction`    | `CandidateShortlistedEvent` creates audit log with `CandidateShortlisted` | `LogAsync` called with correct action and entity type     |
| `FinalInterviewScheduledAuditHandler_LogsCorrectAction` | `FinalInterviewScheduledEvent` creates correct audit log                  | `LogAsync` called with correct action and entity type     |
| `OfferExtendedAuditHandler_LogsCorrectAction`           | `OfferExtendedEvent` creates correct audit log                            | `LogAsync` called with correct action and entity type     |

**Why:** These handlers are the only bridge between domain events and the audit trail. If any handler maps the wrong action or entity type, the audit log becomes misleading. Each handler is tested individually because they map different event properties.

---

#### `TenantProvisionedHandlerTests` (3 tests)

Tests the `TenantProvisionedHandler` that seeds default `CompanySettings` when a new tenant is provisioned.

| Test                                           | What It Verifies                                                                    | Expected Outcome                                       |
| ---------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------ |
| `Handle_NewTenant_SeedsDefaultCompanySettings` | Default settings created with correct `TenantId` and default values                 | Settings entity added to repository                    |
| `Handle_NewTenant_CreatesAuditLogEntry`        | `TenantProvisioned` audit log entry created after seeding                           | `LogAsync` called with `AuditAction.TenantProvisioned` |
| `Handle_NewTenant_DefaultScreeningCriteria`    | Default screening criteria (Skills 40%, Experience 30%, Education 15%, Quality 15%) | JSON contains all 4 criteria with correct weights      |

**Why:** `TenantProvisionedHandler` is the only way `CompanySettings` gets created. If seeding fails or defaults are wrong, the Admin settings endpoints will return 404 for every new tenant.

---

#### `AuditConstantsTests` (6 tests)

Tests `AuditAction` and `AuditEntityType` validation methods via `[Theory]` with `[InlineData]`.

| Test                                                     | What It Verifies                            | Expected Outcome |
| -------------------------------------------------------- | ------------------------------------------- | ---------------- |
| `AuditAction_IsValid_ValidAction_ReturnsTrue`            | All 8 valid actions pass validation         | Returns `true`   |
| `AuditAction_IsValid_InvalidAction_ReturnsFalse`         | Invalid/lowercase actions are rejected      | Returns `false`  |
| `AuditAction_IsValid_EmptyAction_ReturnsFalse`           | Empty string is rejected                    | Returns `false`  |
| `AuditEntityType_IsValid_ValidEntityType_ReturnsTrue`    | All 7 valid entity types pass validation    | Returns `true`   |
| `AuditEntityType_IsValid_InvalidEntityType_ReturnsFalse` | Invalid/lowercase entity types are rejected | Returns `false`  |
| `AuditEntityType_IsValid_EmptyEntityType_ReturnsFalse`   | Empty string is rejected                    | Returns `false`  |

**Why:** Audit constants must match values stored in PostgreSQL. If `IsValid()` accepts values that the database rejects (or vice versa), audit log writes will fail at runtime.

---

#### `UpdateCompanySettingsRequestValidatorTests` (11 tests)

Tests FluentValidation rules for the `UpdateCompanySettingsRequest` merge-patch DTO.

| Test                                              | What It Verifies                                 | Expected Outcome           |
| ------------------------------------------------- | ------------------------------------------------ | -------------------------- |
| `Validate_EmptyRequest_IsValid`                   | All-null request is valid (no-op patch)          | `IsValid` is true          |
| `Validate_ValidTimezone_IsValid`                  | Valid timezone string passes                     | `IsValid` is true          |
| `Validate_TooLongTimezone_HasError`               | Timezone over 50 chars is rejected               | Error on `DefaultTimezone` |
| `Validate_ValidCurrency_IsValid`                  | 3-letter currency code passes                    | `IsValid` is true          |
| `Validate_InvalidCurrencyLength_HasError`         | Currency code not exactly 3 chars is rejected    | Error on `DefaultCurrency` |
| `Validate_PasswordMinLength_TooLow_HasError`      | Password min length below 6 is rejected          | Error on nested path       |
| `Validate_PasswordMinLength_TooHigh_HasError`     | Password min length above 128 is rejected        | Error on nested path       |
| `Validate_ScreeningThreshold_OutOfRange_HasError` | Screening threshold outside 0–100 is rejected    | Error on nested path       |
| `Validate_MatchingWeight_OutOfRange_HasError`     | Matching weight outside 0–100 is rejected        | Error on nested path       |
| `Validate_InvalidPassFailPolicy_HasError`         | Invalid pass/fail policy enum string is rejected | Error on nested path       |
| `Validate_ValidCompleteRequest_IsValid`           | Fully populated valid request passes all rules   | `IsValid` is true          |

**Why:** The validator enforces business constraints (threshold ranges, currency format, password policy bounds) that protect database CHECK constraints and application invariants.

---

### Profiles Module

#### `ProfileConstantsTests` (8 tests)

Tests `FileType.IsValid()` and `SkillLevel.IsValid()` constant validators. Values must match database CHECK constraints and are used for input validation throughout the module.

| Test                                                 | What It Verifies                                              | Expected Outcome |
| ---------------------------------------------------- | ------------------------------------------------------------- | ---------------- |
| `FileType_IsValid_ValidType_ReturnsTrue` [× 2]       | "PDF" and "DOCX" are valid file types                         | Returns `true`   |
| `FileType_IsValid_InvalidType_ReturnsFalse` [× 4]    | "DOC", "TXT", "pdf" (lowercase), and empty string are invalid | Returns `false`  |
| `SkillLevel_IsValid_ValidLevel_ReturnsTrue` [× 4]    | Beginner/Intermediate/Advanced/Expert are valid               | Returns `true`   |
| `SkillLevel_IsValid_InvalidLevel_ReturnsFalse` [× 4] | Lowercase, uppercase, and unknown values are invalid          | Returns `false`  |

**Why:** PascalCase validation is critical — the database CHECK constraint rejects lowercase variants. These tests catch casing mismatches before they reach PostgreSQL.

---

#### `ProfileServiceTests` (5 tests)

Tests `ProfileService` — the application service for profile CRUD. Uses NSubstitute to mock `IApplicantProfileRepository` and keyed `IUnitOfWork`.

| Test                                                    | What It Verifies                                       | Expected Outcome                                     |
| ------------------------------------------------------- | ------------------------------------------------------ | ---------------------------------------------------- |
| `GetByUserIdAsync_ProfileExists_ReturnsProfileResponse` | Existing profile maps entity to response DTO correctly | Response has matching userId, firstName, lastName    |
| `GetByUserIdAsync_ProfileNotFound_ThrowsAppError`       | Missing profile throws `PROFILE_NOT_FOUND`             | Throws `AppError` with code `PROFILE_NOT_FOUND`      |
| `CreateAsync_ValidRequest_CreatesAndReturnsProfile`     | Happy path: profile created with userId as PK          | `Add()` and `SaveChangesAsync()` called, DTO match   |
| `CreateAsync_ProfileAlreadyExists_ThrowsAppError`       | Duplicate profile throws `PROFILE_ALREADY_EXISTS`      | Throws `AppError` with code `PROFILE_ALREADY_EXISTS` |
| `UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields`    | JSON merge patch: only non-null fields are applied     | Updated field changes, others remain unchanged       |
| `UpdateAsync_ProfileNotFound_ThrowsAppError`            | Missing profile on update throws `PROFILE_NOT_FOUND`   | Throws `AppError` with code `PROFILE_NOT_FOUND`      |

**Why:** Profile CRUD is the primary way applicants interact with their data. Merge patch semantics must not accidentally null out unrelated fields. The shared PK with `auth.users` means `CreateAsync` must use the userId as the entity ID.

---

#### `ResumeServiceTests` (8 tests)

Tests `ResumeService` — upload, listing, and retrieval. Uses NSubstitute to mock `IResumeRepository`, `IFileStorage`, `IEventPublisher`, and keyed `IUnitOfWork`.

| Test                                                             | What It Verifies                                                          | Expected Outcome                             |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------- | -------------------------------------------- |
| `UploadResumeAsync_ValidPdf_UploadsAndPublishesEvent`            | PDF upload: stores file, persists resume, publishes `ResumeUploadedEvent` | Event published with correct userId/tenantId |
| `UploadResumeAsync_ValidDocx_UploadsSuccessfully`                | DOCX upload resolves file type correctly                                  | FileType is "DOCX"                           |
| `UploadResumeAsync_InvalidFileType_ThrowsAppError`               | .txt file rejected before storage                                         | Throws `INVALID_REQUEST`                     |
| `UploadResumeAsync_FileTooLarge_ThrowsAppError`                  | 26 MB file exceeds 25 MB limit                                            | Throws `INVALID_REQUEST`                     |
| `UploadResumeAsync_MarksPreviousResumesAsNotLatest`              | Previous resumes marked not-latest before adding new one                  | `MarkPreviousAsNotLatestAsync` called once   |
| `GetResumesAsync_ReturnsAllResumesForUser`                       | Returns all resumes for a user                                            | List has expected count                      |
| `GetResumeByIdAsync_ResumeExistsAndBelongsToUser_ReturnsResume`  | Resume returned when owned by requesting user                             | Response has matching ID                     |
| `GetResumeByIdAsync_ResumeNotFound_ThrowsAppError`               | Missing resume throws `RESUME_NOT_FOUND`                                  | Throws `RESUME_NOT_FOUND`                    |
| `GetResumeByIdAsync_ResumeBelongsToDifferentUser_ThrowsAppError` | Resume owned by another user treated as not found                         | Throws `RESUME_NOT_FOUND`                    |

**Why:** The authorization check (`resume.UserId != userId`) prevents cross-user data access. The "marks previous as not latest" behavior ensures only one resume per user is the active version. Event publishing triggers async parsing via MassTransit.

---

#### `UserRegisteredProfileHandlerTests` (5 tests)

Tests the MediatR handler that auto-creates an empty `ApplicantProfile` when a user registers with the Applicant role.

| Test                                                 | What It Verifies                                          | Expected Outcome                |
| ---------------------------------------------------- | --------------------------------------------------------- | ------------------------------- |
| `Handle_ApplicantRole_CreatesEmptyProfile`           | Applicant registration creates a profile with empty names | Profile added with userId as PK |
| `Handle_NonApplicantRole_SkipsProfileCreation` [× 3] | AgencyAdmin/HiringManager/Recruiter skip profile creation | `Add()` not called              |
| `Handle_ProfileAlreadyExists_SkipsCreation`          | Idempotent: existing profile prevents duplicate           | `Add()` not called              |

**Why:** This handler is a cross-module concern triggered by the Auth module's `UserRegisteredEvent`. It must only create profiles for Applicants and be idempotent to handle event redelivery.

---

#### `CreateProfileRequestValidatorTests` (9 tests)

Tests `CreateProfileRequestValidator` — FluentValidation rules for new profile creation.

| Test                                    | What It Verifies                       | Expected Outcome     |
| --------------------------------------- | -------------------------------------- | -------------------- |
| `Validate_ValidRequest_Passes`          | Fully valid request passes all rules   | `IsValid` is true    |
| `Validate_EmptyFirstName_Fails` [× 2]   | Empty/null first name is rejected      | Error on `FirstName` |
| `Validate_FirstNameTooLong_Fails`       | First name over 100 chars is rejected  | Error on `FirstName` |
| `Validate_EmptyLastName_Fails` [× 2]    | Empty/null last name is rejected       | Error on `LastName`  |
| `Validate_PhoneTooLong_Fails`           | Phone over 20 chars is rejected        | Error on `Phone`     |
| `Validate_SkillWithInvalidLevel_Fails`  | Invalid skill level string is rejected | Error on level path  |
| `Validate_SkillWithValidLevel_Passes`   | Valid skill level passes               | `IsValid` is true    |
| `Validate_SkillWithEmptyName_Fails`     | Empty skill name is rejected           | Error on name path   |
| `Validate_SkillWithNegativeYears_Fails` | Negative skill years is rejected       | Error on years path  |

**Why:** Validation rules protect database column constraints (varchar lengths, CHECK constraints on skill levels) and ensure consistent data entry.

---

#### `UpdateProfileRequestValidatorTests` (6 tests)

Tests `UpdateProfileRequestValidator` — FluentValidation rules for the merge-patch DTO. All fields optional, but non-empty when provided.

| Test                                        | What It Verifies                                     | Expected Outcome     |
| ------------------------------------------- | ---------------------------------------------------- | -------------------- |
| `Validate_AllFieldsNull_Passes`             | All-null request is valid (no-op patch)              | `IsValid` is true    |
| `Validate_ValidPartialUpdate_Passes`        | Partial update with valid fields passes              | `IsValid` is true    |
| `Validate_EmptyFirstNameWhenProvided_Fails` | Empty string first name rejected (must be non-empty) | Error on `FirstName` |
| `Validate_EmptyLastNameWhenProvided_Fails`  | Empty string last name rejected                      | Error on `LastName`  |
| `Validate_FirstNameTooLong_Fails`           | First name over 100 chars is rejected                | Error on `FirstName` |
| `Validate_SkillWithInvalidLevel_Fails`      | Invalid skill level string is rejected               | Error on level path  |

**Why:** Merge patch validators must allow fully-null requests (no-op), but when a field is provided it must still meet all constraints. This prevents clients from accidentally clearing required fields.

---

#### `AiResumeParserClientTests` (5 tests)

Tests `AiResumeParserClient` — the HTTP client for the AI Service's resume parsing endpoint. Uses a fake `HttpMessageHandler` to simulate various failure modes.

| Test                                          | What It Verifies                                                  | Expected Outcome |
| --------------------------------------------- | ----------------------------------------------------------------- | ---------------- |
| `ParseAsync_SuccessResponse_ReturnsResult`    | 200 OK with valid JSON returns deserialized `AiResumeParseResult` | Non-null result  |
| `ParseAsync_ErrorResponse_ReturnsNull`        | 500 response returns null (graceful fallback)                     | Returns `null`   |
| `ParseAsync_HttpRequestException_ReturnsNull` | Connection refused returns null (AI Service down)                 | Returns `null`   |
| `ParseAsync_TaskCanceled_ReturnsNull`         | Request timeout returns null (resilience policy triggered)        | Returns `null`   |
| `ParseAsync_InvalidJson_ReturnsNull`          | Malformed JSON response returns null                              | Returns `null`   |

**Why:** The AI parser client must never throw exceptions to the consumer. All failure modes must be handled gracefully with null return, allowing the basic parser output to be used as the sole result. This ensures resume processing is never blocked by AI Service availability.

---

### Recruitment Module

#### `JobPostingTests` (4 tests)

Tests the `JobPosting` aggregate root's domain behavior — status lifecycle transitions and default state.

| Test                                                  | What It Verifies                                                      | Expected Outcome                            |
| ----------------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------- |
| `Publish_DraftJobPosting_SetsStatusAndTimestamp`       | Publishing sets status to `Published` and records `PublishedAt`       | Status is Published, PublishedAt is not null |
| `Close_PublishedJobPosting_SetsStatusAndTimestamp`     | Closing sets status to `Closed` and records `ClosedAt`               | Status is Closed, ClosedAt is not null      |
| `NewJobPosting_HasDefaultDraftStatus`                  | A new job posting starts in Draft status                             | Status is `Draft`                           |
| `NewJobPosting_HasEmptyCollections`                    | Criteria and Questions collections are initialized empty             | Both collections are empty                  |

**Why:** Job postings follow a strict Draft → Published → Closed lifecycle. If status transitions set incorrect values or timestamps aren't recorded, the entire recruitment pipeline breaks — applications can only be submitted to Published postings.

---

#### `ApplicationTests` (6 tests)

Tests the `Application` aggregate root's domain behavior — submission events and withdrawal.

| Test                                                       | What It Verifies                                                          | Expected Outcome                          |
| ---------------------------------------------------------- | ------------------------------------------------------------------------- | ----------------------------------------- |
| `Submit_RaisesDomainEvent`                                 | Submitting raises `ApplicationSubmittedEvent`                            | DomainEvents has exactly 1 event          |
| `Submit_WithQuestionAnswers_SetsAnswers`                    | Submission with question answers stores them                             | QuestionAnswers collection is populated   |
| `Submit_Event_ContainsCorrectIds`                           | The domain event carries the correct application and job posting IDs     | Event IDs match entity IDs                |
| `Withdraw_SetsStatusAndTimestamp`                           | Withdrawing sets status to `Withdrawn` and records `WithdrawnAt`         | Status is Withdrawn, WithdrawnAt is set   |
| `NewApplication_HasDefaultSubmittedStatus`                  | A new application starts in Submitted status                             | Status is `Submitted`                     |
| `Submit_WithoutQuestionAnswers_LeavesCollectionEmpty`       | Submission without answers leaves QuestionAnswers empty                  | QuestionAnswers is empty                  |

**Why:** `ApplicationSubmittedEvent` is the trigger for the entire downstream pipeline (Screening → Assessment → Shortlisting). If this event isn't raised or carries wrong IDs, screening never starts.

---

#### `CreateJobPostingRequestValidatorTests` (10 tests)

Tests FluentValidation rules for the `CreateJobPostingRequest` DTO.

| Test                                                         | What It Verifies                                                         | Expected Outcome     |
| ------------------------------------------------------------ | ------------------------------------------------------------------------ | -------------------- |
| `Validate_ValidRequest_Passes`                               | A well-formed request passes all validation rules                        | No errors            |
| `Validate_EmptyTitle_Fails`                                  | Empty title is rejected                                                  | Validation error     |
| `Validate_TitleTooLong_Fails`                                | Title exceeding 200 chars is rejected                                    | Validation error     |
| `Validate_InvalidLocationType_Fails`                         | Invalid location type value is rejected                                  | Validation error     |
| `Validate_OnSiteWithoutCity_Fails`                           | OnSite location without city is rejected                                 | Validation error     |
| `Validate_RemoteWithoutCity_Passes`                          | Remote location without city is acceptable                               | No errors            |
| `Validate_InvalidEmploymentType_Fails`                       | Invalid employment type value is rejected                                | Validation error     |
| `Validate_SalaryMinGreaterThanMax_Fails`                     | Salary min > max is rejected                                             | Validation error     |
| `Validate_SalaryWithoutCurrency_Fails`                       | Providing salary without currency is rejected                            | Validation error     |
| `Validate_NegativeSalary_Fails`                              | Negative salary value is rejected                                        | Validation error     |

**Why:** These rules enforce database CHECK constraints and business invariants at the API boundary. Invalid data that passes validation would cause DB constraint violations or corrupt data.

---

#### `SubmitApplicationRequestValidatorTests` (4 tests)

Tests FluentValidation rules for the `SubmitApplicationRequest` DTO.

| Test                                                     | What It Verifies                                                | Expected Outcome |
| -------------------------------------------------------- | --------------------------------------------------------------- | ---------------- |
| `Validate_ValidRequest_Passes`                           | A well-formed submission passes                                 | No errors        |
| `Validate_EmptyResumeId_Fails`                           | Empty resume ID is rejected                                     | Validation error |
| `Validate_EmptyQuestionId_InAnswers_Fails`               | Question answer with empty question ID is rejected              | Validation error |
| `Validate_NullQuestionAnswers_Passes`                    | Null question answers (no screening questions) is acceptable    | No errors        |

**Why:** Resume ID is required for every application. Question answers with empty IDs would create orphaned responses that can't be scored.

---

#### `RecruitmentServiceTests` (11 tests)

Tests the `RecruitmentService` which manages job posting CRUD and lifecycle transitions.

| Test                                                             | What It Verifies                                                       | Expected Outcome              |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------- | ----------------------------- |
| `CreateAsync_ValidRequest_CreatesJobPosting`                     | Creates a job posting with correct properties                          | Posting saved to repository   |
| `CreateAsync_WithClientCompany_SetsCompanyId`                    | Client company ID is validated and set                                 | Company ID matches            |
| `CreateAsync_InvalidClientCompany_ThrowsNotFound`                | Non-existent client company throws error                               | AppError thrown                |
| `GetByIdAsync_ExistingPosting_ReturnsResponse`                   | Returns mapped response for existing posting                           | Response matches entity       |
| `GetByIdAsync_NonExistent_ThrowsNotFound`                        | Non-existent ID throws not found error                                 | AppError thrown                |
| `UpdateAsync_ValidRequest_UpdatesFields`                         | JSON merge patch applies only non-null fields                          | Updated fields match request  |
| `UpdateAsync_NonExistent_ThrowsNotFound`                         | Update on non-existent posting throws error                            | AppError thrown                |
| `PublishAsync_DraftPosting_SetsPublished`                        | Draft posting transitions to Published                                 | Status is Published           |
| `PublishAsync_NonDraftPosting_ThrowsInvalidStatus`               | Publishing non-Draft posting throws error                              | AppError thrown                |
| `PublishAsync_NonExistent_ThrowsNotFound`                        | Publishing non-existent posting throws error                           | AppError thrown                |
| `CloseAsync_PublishedPosting_SetsClosed`                         | Published posting transitions to Closed                                | Status is Closed              |

**Why:** The service enforces the job posting lifecycle (Draft → Published → Closed) which gates the entire application flow. Invalid transitions would allow applications to non-published jobs or modifications to published postings.

---

#### `ApplicationServiceTests` (10 tests)

Tests the `ApplicationService` which handles application submission, retrieval, and withdrawal.

| Test                                                              | What It Verifies                                                         | Expected Outcome              |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------ | ----------------------------- |
| `SubmitAsync_ValidApplication_CreatesAndPublishesEvent`           | Successful submission saves application and raises domain event          | Application saved, event raised|
| `SubmitAsync_NonPublishedPosting_ThrowsInvalidStatus`             | Submitting to non-Published job throws error                             | AppError thrown                |
| `SubmitAsync_NonExistentPosting_ThrowsNotFound`                   | Submitting to non-existent job throws error                              | AppError thrown                |
| `SubmitAsync_DuplicateApplication_ThrowsDuplicate`                | Same applicant applying twice throws error                               | AppError thrown                |
| `SubmitAsync_ResumeNotOwned_ThrowsForbidden`                      | Using another user's resume throws error                                 | AppError thrown                |
| `GetByIdAsync_ExistingApplication_ReturnsResponse`                | Returns mapped response for existing application                         | Response matches entity       |
| `GetByIdAsync_NonExistent_ThrowsNotFound`                         | Non-existent application throws error                                    | AppError thrown                |
| `WithdrawAsync_OwnApplication_SetsWithdrawn`                      | Applicant can withdraw their own application                             | Status is Withdrawn           |
| `WithdrawAsync_OtherUsersApplication_ThrowsForbidden`             | Withdrawing another user's application throws error                      | AppError thrown                |
| `WithdrawAsync_AlreadyWithdrawn_ThrowsInvalidStatus`              | Withdrawing an already withdrawn application throws error                | AppError thrown                |

**Why:** Application submission is the entry point to the recruitment pipeline. The one-per-person-per-job constraint, resume ownership validation, and posting status checks prevent data corruption and unauthorized access.

---

#### `ClientCompanyServiceTests` (5 tests)

Tests the `ClientCompanyService` CRUD operations.

| Test                                                   | What It Verifies                                         | Expected Outcome           |
| ------------------------------------------------------ | -------------------------------------------------------- | -------------------------- |
| `CreateAsync_ValidRequest_CreatesCompany`              | Creates client company with correct properties           | Company saved to repo      |
| `GetByIdAsync_ExistingCompany_ReturnsResponse`         | Returns mapped response for existing company             | Response matches entity    |
| `GetByIdAsync_NonExistent_ThrowsNotFound`              | Non-existent company throws error                        | AppError thrown             |
| `UpdateAsync_ValidRequest_AppliesMergePatch`           | JSON merge patch applies only non-null fields            | Updated fields match       |
| `UpdateAsync_NonExistent_ThrowsNotFound`               | Update on non-existent company throws error              | AppError thrown             |

**Why:** Client companies support the agency model. Invalid CRUD operations would corrupt the relationship between job postings and their client companies.

---

#### `CriteriaServiceTests` (8 tests)

Tests the `CriteriaService` for managing evaluation criteria and AI-assisted suggestions.

| Test                                                          | What It Verifies                                                  | Expected Outcome           |
| ------------------------------------------------------------- | ----------------------------------------------------------------- | -------------------------- |
| `AddAsync_ValidCriteria_AddsToCriteria`                       | Creates criterion linked to job posting                           | Criterion saved            |
| `AddAsync_NonExistentJob_ThrowsNotFound`                      | Adding criteria to non-existent job throws error                  | AppError thrown             |
| `UpdateAsync_ValidUpdate_AppliesMergePatch`                   | JSON merge patch updates only provided fields                     | Updated fields match       |
| `UpdateAsync_WrongJob_ThrowsNotFound`                         | Updating criterion from another job throws error                  | AppError thrown             |
| `DeleteAsync_ExistingCriteria_Removes`                        | Deletes criterion successfully                                    | Repository Remove called   |
| `DeleteAsync_NonExistent_ThrowsNotFound`                      | Deleting non-existent criterion throws error                      | AppError thrown             |
| `SuggestAsync_AiAvailable_ReturnsSuggestions`                 | AI service returns suggestions when available                     | Suggestions returned       |
| `SuggestAsync_AiUnavailable_ReturnsNull`                      | Returns null when AI service is unavailable                       | Returns null               |

**Why:** Evaluation criteria drive the Screening module's scoring engine. The weights, categories, and evaluation methods must be valid and correctly linked to job postings for accurate candidate scoring.

---

#### `ScreeningQuestionServiceTests` (8 tests)

Tests the `ScreeningQuestionService` for managing screening questions and feature-gated AI suggestions.

| Test                                                               | What It Verifies                                                      | Expected Outcome        |
| ------------------------------------------------------------------ | --------------------------------------------------------------------- | ----------------------- |
| `AddAsync_ValidQuestion_AddsToQuestions`                           | Creates question linked to job posting                                | Question saved          |
| `AddAsync_NonExistentJob_ThrowsNotFound`                           | Adding question to non-existent job throws error                      | AppError thrown          |
| `UpdateAsync_ValidUpdate_AppliesMergePatch`                        | JSON merge patch updates only provided fields                         | Updated fields match    |
| `UpdateAsync_WrongJob_ThrowsNotFound`                              | Updating question from another job throws error                       | AppError thrown          |
| `DeleteAsync_ExistingQuestion_Removes`                             | Deletes question successfully                                         | Repository Remove called|
| `DeleteAsync_NonExistent_ThrowsNotFound`                           | Deleting non-existent question throws error                           | AppError thrown          |
| `SuggestAsync_FeatureDisabled_ReturnsNull`                         | Returns null when tenant setting disables AI questions                 | Returns null            |
| `SuggestAsync_FeatureEnabled_ReturnsSuggestions`                   | AI service returns suggestions when feature is enabled                | Suggestions returned    |

**Why:** Screening questions are the foundation of the assessment flow. The feature gate for AI suggestions ensures tenant-level control over AI features. Questions with wrong job posting links would cause assessment to fail.

---

#### `RecruitmentConstantsTests` (22 tests)

Tests `IsValid()` methods for all 11 Recruitment module constant classes. Values must match PostgreSQL CHECK constraints exactly.

| Constant Class        | Tests | What It Verifies                                                |
| --------------------- | ----- | --------------------------------------------------------------- |
| `JobPostingStatus`    | 2     | Draft/Published/Closed valid; lowercase and unknown values invalid |
| `ApplicationStatus`   | 2     | All 9 statuses valid; lowercase and unknown values invalid      |
| `LocationType`        | 2     | OnSite/Remote/Hybrid valid; lowercase and unknown values invalid|
| `EmploymentType`      | 2     | All 5 types valid; lowercase and unknown values invalid         |
| `CriteriaCategory`    | 2     | All 6 categories valid; lowercase and unknown values invalid    |
| `EvaluationMethod`    | 2     | All 3 methods valid; lowercase and unknown values invalid       |
| `QuestionType`        | 2     | All 3 types valid; lowercase and unknown values invalid         |
| `QuestionTiming`      | 2     | AtApplication/AfterScreening valid; lowercase and unknown invalid|
| `RejectedAtStage`     | 2     | All 5 stages valid; lowercase and unknown values invalid        |
| `ClientCompanyStatus` | 2     | Active/Inactive valid; lowercase and unknown values invalid     |

**Why:** PascalCase enum values must exactly match PostgreSQL CHECK constraints. Lowercase or unknown values that pass `IsValid()` would violate DB constraints. These tests guard every status/type field in the module.

---

#### `AiCriteriaSuggesterClientTests` (5 tests)

Tests the `AiCriteriaSuggesterClient` HTTP client that calls the AI Service for criteria suggestions.

| Test                                                   | What It Verifies                                              | Expected Outcome    |
| ------------------------------------------------------ | ------------------------------------------------------------- | ------------------- |
| `SuggestAsync_SuccessResponse_ReturnsSuggestions`      | Successful AI response returns deserialized suggestions       | Suggestions list    |
| `SuggestAsync_ErrorResponse_ReturnsNull`               | Non-success HTTP status returns null                          | Returns null        |
| `SuggestAsync_HttpRequestException_ReturnsNull`        | Network errors return null                                    | Returns null        |
| `SuggestAsync_TaskCanceled_ReturnsNull`                | Timeout returns null                                          | Returns null        |
| `SuggestAsync_InvalidJson_ReturnsNull`                 | Malformed JSON response returns null                          | Returns null        |

**Why:** The AI criteria client must never throw exceptions to callers. All failure modes return null, allowing the application to gracefully handle AI Service unavailability by returning 204 No Content to the user.

---

#### `AiQuestionSuggesterClientTests` (5 tests)

Tests the `AiQuestionSuggesterClient` HTTP client that calls the AI Service for screening question suggestions.

| Test                                                   | What It Verifies                                              | Expected Outcome    |
| ------------------------------------------------------ | ------------------------------------------------------------- | ------------------- |
| `SuggestAsync_SuccessResponse_ReturnsSuggestions`      | Successful AI response returns deserialized suggestions       | Suggestions list    |
| `SuggestAsync_ErrorResponse_ReturnsNull`               | Non-success HTTP status returns null                          | Returns null        |
| `SuggestAsync_HttpRequestException_ReturnsNull`        | Network errors return null                                    | Returns null        |
| `SuggestAsync_TaskCanceled_ReturnsNull`                | Timeout returns null                                          | Returns null        |
| `SuggestAsync_InvalidJson_ReturnsNull`                 | Malformed JSON response returns null                          | Returns null        |

**Why:** Same resilience pattern as the criteria client. AI question suggestions are feature-gated and optional — failures must never block the core recruitment workflow.

---

## Architecture Tests (`Jobsite.ArchitectureTests`)

Architecture tests enforce structural rules at build time using NetArchTest. They prevent architectural drift as the codebase grows.

### `LayerDependencyTests` (10 tests)

Enforces the module layer dependency direction: `Domain → SharedKernel only`, `Application → Domain only`. Covers Tenancy and Profiles modules.

| Test                                                         | What It Verifies                                                          | Expected Outcome         |
| ------------------------------------------------------------ | ------------------------------------------------------------------------- | ------------------------ |
| `DomainLayer_ShouldNotReference_ApplicationLayer`            | Tenancy.Domain has no dependency on Tenancy.Application                   | No violating types found |
| `DomainLayer_ShouldNotReference_InfrastructureLayer`         | Tenancy.Domain has no dependency on Tenancy.Infrastructure                | No violating types found |
| `DomainLayer_ShouldNotReference_EFCore`                      | Tenancy.Domain has no dependency on `Microsoft.EntityFrameworkCore`       | No violating types found |
| `ApplicationLayer_ShouldNotReference_InfrastructureLayer`    | Tenancy.Application has no dependency on Tenancy.Infrastructure           | No violating types found |
| `ApplicationLayer_ShouldNotReference_EFCore`                 | Tenancy.Application has no dependency on `Microsoft.EntityFrameworkCore`  | No violating types found |
| `ProfilesDomain_ShouldNotReference_ApplicationLayer`         | Profiles.Domain has no dependency on Profiles.Application                 | No violating types found |
| `ProfilesDomain_ShouldNotReference_InfrastructureLayer`      | Profiles.Domain has no dependency on Profiles.Infrastructure              | No violating types found |
| `ProfilesDomain_ShouldNotReference_EFCore`                   | Profiles.Domain has no dependency on `Microsoft.EntityFrameworkCore`      | No violating types found |
| `ProfilesApplication_ShouldNotReference_InfrastructureLayer` | Profiles.Application has no dependency on Profiles.Infrastructure         | No violating types found |
| `ProfilesApplication_ShouldNotReference_EFCore`              | Profiles.Application has no dependency on `Microsoft.EntityFrameworkCore` | No violating types found |

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

### Auth Module

#### Fixture

- `AuthIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `AuthDbContext`, runs `InitialAuthSchema` migration. Exposes `ConnectionString` property for direct database access.
- `AuthIntegrationCollection` — xUnit `[Collection("Auth")]` for shared container across Auth test classes

#### `UserRepositoryTests` (14 tests)

Tests `UserRepository` against a real PostgreSQL database. Validates EF Core configurations, snake_case mapping, CHECK constraints, unique indexes, and query behavior for the `auth.users` table.

| Test                                                                     | What It Verifies                                                                     | Expected Outcome                                                         |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------ |
| `Add_ValidUser_PersistsToDatabase`                                       | `Add()` + `SaveChangesAsync()` inserts a user with DB-assigned UUID and timestamps   | Re-queried user has non-empty `Id`, `CreatedAt`/`UpdatedAt` close to now |
| `GetByIdAsync_ExistingUser_ReturnsUser`                                  | ID-based lookup returns the correct user                                             | Email matches                                                            |
| `GetByIdAsync_NonExistentId_ReturnsNull`                                 | Random GUID returns null, not exception                                              | Returns `null`                                                           |
| `GetByEmailAsync_ExistingEmail_ReturnsUser`                              | Email-based lookup returns the correct user                                          | Email matches                                                            |
| `GetByEmailAsync_NonExistentEmail_ReturnsNull`                           | Missing email returns null                                                           | Returns `null`                                                           |
| `GetByEmailForUpdateAsync_ExistingEmail_ReturnsTrackedUser`              | Returns a tracked (not `AsNoTracking`) user for mutation                             | Entity is in change tracker                                              |
| `EmailExistsAsync_ExistingEmail_ReturnsTrue`                             | Existence check for a persisted email                                                | Returns `true`                                                           |
| `EmailExistsAsync_NonExistentEmail_ReturnsFalse`                         | Existence check for a missing email                                                  | Returns `false`                                                          |
| `GetByExternalLoginAsync_ExistingProvider_ReturnsUserWithExternalLogins` | OAuth lookup by provider + subject ID returns user with eager-loaded external logins | User has 1 external login with matching subject ID                       |
| `GetByExternalLoginAsync_NonExistentProvider_ReturnsNull`                | Missing provider/subject returns null                                                | Returns `null`                                                           |
| `Add_DuplicateEmail_ThrowsDbUpdateException`                             | Unique index `ix_users_email` rejects duplicate emails                               | Throws `DbUpdateException`                                               |
| `Add_InvalidRole_ThrowsDbUpdateException`                                | CHECK constraint `chk_users_role` rejects invalid role (e.g., "SuperAdmin")          | Throws `DbUpdateException`                                               |
| `Add_InvalidStatus_ThrowsDbUpdateException`                              | CHECK constraint `chk_users_status` rejects invalid status (e.g., "Banned")          | Throws `DbUpdateException`                                               |
| `Add_AllValidRoles_PersistSuccessfully`                                  | All 5 valid roles pass the CHECK constraint                                          | No exception thrown                                                      |
| `Add_AllValidStatuses_PersistSuccessfully`                               | All 3 valid statuses pass the CHECK constraint                                       | No exception thrown                                                      |

**Why:** Repository integration tests against real PostgreSQL catch EF Core misconfigurations that unit tests with mocked repositories cannot — wrong column types, missing indexes, broken relationships, or CHECK constraint mismatches. The `GetByEmailForUpdateAsync` tracking test ensures mutations won't silently fail due to `AsNoTracking`.

---

#### `RefreshTokenRepositoryTests` (7 tests)

Tests `RefreshTokenRepository` against a real PostgreSQL database. Validates token persistence, hash lookups, family-based revocation logic, unique constraints, and cascade behavior.

| Test                                                         | What It Verifies                                                                           | Expected Outcome                                       |
| ------------------------------------------------------------ | ------------------------------------------------------------------------------------------ | ------------------------------------------------------ |
| `Add_ValidToken_PersistsToDatabase`                          | Token persists with correct `UserId`, `TokenHash`, `FamilyId`, and defaults                | Re-queried token exists, `IsRevoked` is false          |
| `GetByTokenHashAsync_ExistingHash_ReturnsToken`              | Hash-based lookup returns the correct token                                                | `TokenHash` and `UserId` match                         |
| `GetByTokenHashAsync_NonExistentHash_ReturnsNull`            | Missing hash returns null                                                                  | Returns `null`                                         |
| `RevokeFamilyAsync_MultipleFamilyTokens_RevokesAllInFamily`  | Family revocation revokes all tokens in the target family but not tokens in other families | Family tokens revoked, other family's tokens untouched |
| `RevokeFamilyAsync_AlreadyRevokedTokens_SkipsAlreadyRevoked` | Already-revoked tokens are ignored, only active tokens in the family are revoked           | Active token gets revoked                              |
| `Add_DuplicateTokenHash_ThrowsDbUpdateException`             | Unique index `ix_refresh_tokens_token_hash` rejects duplicate hashes                       | Throws `DbUpdateException`                             |
| `CascadeDelete_WhenUserDeleted_DeletesRefreshTokens`         | Deleting a user cascade-deletes all their refresh tokens                                   | Token no longer found after user deletion              |

**Why:** The refresh token family revocation is the core of replay detection — if a stolen token is reused, the entire family must be revoked. These tests validate that the SQL `WHERE family_id = @id AND is_revoked = false` query works correctly against real PostgreSQL, and that cascade deletes properly clean up tokens when a user is removed.

---

#### `AuthDbContextTests` (5 tests)

Tests AuthDbContext schema creation, table mapping, default values, and relationship behavior. Validates the `auth` schema exists and EF Core configurations produce correct database structures.

| Test                                                      | What It Verifies                                                                  | Expected Outcome                                         |
| --------------------------------------------------------- | --------------------------------------------------------------------------------- | -------------------------------------------------------- |
| `Schema_AuthSchemaExists`                                 | The `auth` PostgreSQL schema is created by the migration                          | Schema `auth` found in `information_schema.schemata`     |
| `Users_DefaultValues_AppliedByDatabase`                   | `id`, `email_verified`, `created_at`, `updated_at` defaults are applied by the DB | UUID generated, `false` default, timestamps close to now |
| `Users_ExternalLoginCascade_DeletesLoginWhenUserDeleted`  | Cascade delete removes external logins when user is deleted                       | Login record no longer found                             |
| `ExternalLogins_UniqueProviderPerUser_EnforcedByDatabase` | Unique index on (provider, subject_id) rejects duplicate OAuth provider links     | Throws `DbUpdateException`                               |
| `Users_NullablePasswordHash_AllowsOAuthOnlyUsers`         | `password_hash` column is nullable for OAuth-only users (no email/password)       | User persisted with `null` password hash                 |

**Why:** These tests ensure the database schema matches the design spec: auth schema isolation, correct defaults, proper cascading, and nullable `password_hash` for OAuth-only users. The unique provider constraint test validates that a user can't accidentally link the same OAuth account twice.

---

### Admin Module

#### Fixture

- `AdminIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `AdminDbContext`, runs `InitialAdminSchema` migration. Exposes `ConnectionString` property for direct database access.
- `AdminIntegrationCollection` — xUnit `[Collection("Admin")]` for shared container across Admin test classes

#### `AdminDbContextTests` (4 tests)

Tests AdminDbContext schema creation, table mapping, default values, and JSONB column behavior. Validates the `admin` schema exists and EF Core configurations produce correct database structures.

| Test                                              | What It Verifies                                                                  | Expected Outcome                                      |
| ------------------------------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------- |
| `Schema_AdminSchemaExists`                        | The `admin` PostgreSQL schema is created by the migration                         | Schema `admin` found in `information_schema.schemata` |
| `CompanySettings_DefaultValues_AppliedByDatabase` | `id`, `created_at`, `updated_at`, `default_timezone`, `default_currency` defaults | UUID generated, `UTC`, `USD`, timestamps close to now |
| `CompanySettings_JsonbColumns_PersistAndRetrieve` | All 6 JSONB settings columns round-trip correctly                                 | JSON content matches after persist + re-query         |
| `AuditLog_AllColumns_PersistCorrectly`            | All audit log columns persist with correct types and values                       | All fields match after persist + re-query             |

**Why:** EF Core JSONB mapping and schema isolation must be validated against real PostgreSQL. Unit tests with mocks can't catch `jsonb` serialization issues or missing schema configurations.

---

#### `CompanySettingsRepositoryTests` (5 tests)

Tests `CompanySettingsRepository` against a real PostgreSQL database. Validates CRUD operations, tracking behavior, and JSONB persistence.

| Test                                                | What It Verifies                                                      | Expected Outcome                                          |
| --------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------- |
| `Add_ValidSettings_PersistsToDatabase`              | `Add()` + `SaveChangesAsync()` inserts settings with DB-assigned UUID | Re-queried settings have non-empty `Id`, all fields match |
| `GetAsync_ExistingSettings_ReturnsUntracked`        | `GetAsync` returns settings with `AsNoTracking()`                     | Settings returned, entity is NOT in change tracker        |
| `GetAsync_NonExistent_ReturnsNull`                  | Missing tenant settings returns null                                  | Returns `null`                                            |
| `GetForUpdateAsync_ExistingSettings_ReturnsTracked` | `GetForUpdateAsync` returns tracked entity for mutation               | Settings returned, entity IS in change tracker            |
| `GetForUpdateAsync_NonExistent_ReturnsNull`         | Missing tenant settings returns null                                  | Returns `null`                                            |

**Why:** The distinction between tracked and untracked queries is critical — `GetAsync` (read-only, `AsNoTracking`) must not accidentally allow mutations, while `GetForUpdateAsync` must return a tracked entity for the merge-patch update flow to work.

---

#### `AuditLogRepositoryTests` (7 tests)

Tests `AuditLogRepository` against a real PostgreSQL database. Validates cursor-based pagination, filtering, and sort ordering.

| Test                                                     | What It Verifies                                                           | Expected Outcome                                             |
| -------------------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------ |
| `Add_ValidAuditLog_PersistsToDatabase`                   | `Add()` + `SaveChangesAsync()` inserts audit log with DB-assigned UUID     | Re-queried log has non-empty `Id`, all fields match          |
| `GetPageAsync_NoCursor_ReturnsFirstPage`                 | First page returns most recent entries (DESC order) with correct page size | Returns requested page size, entries in descending order     |
| `GetPageAsync_WithCursor_ReturnsNextPage`                | Cursor pagination returns the next page after the cursor position          | Returns entries after the cursor, no overlap with first page |
| `GetPageAsync_FilterByAction_ReturnsOnlyMatchingLogs`    | `action` filter returns only logs with the specified action                | All returned logs have the filtered action                   |
| `GetPageAsync_FilterByActorId_ReturnsOnlyMatchingLogs`   | `actorId` filter returns only logs by the specified actor                  | All returned logs have the filtered actor ID                 |
| `GetPageAsync_FilterByDateRange_ReturnsOnlyMatchingLogs` | `dateFrom`/`dateTo` filter returns only logs within the date range         | All returned logs have `PerformedAt` within range            |
| `GetPageAsync_EmptyResults_ReturnsEmptyWithNullCursor`   | Filtering with no matches returns empty list and null cursor               | Items empty, `NextCursor` null                               |

**Why:** Cursor-based pagination with composite `(performed_at, id)` cursors is complex — off-by-one errors, incorrect sort direction, or broken Base64 encoding would surface only against real PostgreSQL. Filter combinations must also be tested to ensure they compose correctly with pagination.

---

## Test Data Factories

### `TestData.cs` (Unit Tests)

Centralized factory methods for unit test object creation. Avoids inline object construction per `docs/conventions/TESTING_STANDARDS.md`.

| Method                          | Creates                     | Default Values                                       |
| ------------------------------- | --------------------------- | ---------------------------------------------------- |
| `CreateTenant()`                | `Tenant` entity             | Name: "Acme Corp", Subdomain: "acme", Status: Active |
| `CreateTenantBranding()`        | `TenantBranding` entity     | Primary: #1A73E8, Tagline: "Test tagline"            |
| `CreateRegisterTenantRequest()` | `RegisterTenantRequest` DTO | Name: "Acme Corp", Subdomain: "acme"                 |
| `CreateCompanySettings()`       | `CompanySettings` entity    | TenantId: provided, Timezone: UTC, Currency: USD     |
| `CreateAuditLog()`              | `AuditLog` entity           | Action: SettingsUpdated, EntityType: CompanySettings |

All methods accept optional overrides for customization in specific test scenarios.

### `IntegrationTestData.cs` (Integration Tests)

Factory methods generating unique names/subdomains per test to avoid collisions in the shared database.

| Method                  | Creates                    | Default Values                                           |
| ----------------------- | -------------------------- | -------------------------------------------------------- |
| `CreateTenant()`        | `Tenant` entity            | Unique name/subdomain via `Guid`, Status: Active         |
| `CreateBranding()`      | `TenantBranding` entity    | Primary: #1A73E8, Tagline: "Integration test branding"   |
| `CreateUser()`          | `User` entity              | Unique email via `Guid`, Role: Applicant, Status: Active |
| `CreateRefreshToken()`  | `RefreshToken` entity      | Random hash and family, ExpiresAt: 30 days from now      |
| `CreateExternalLogin()` | `UserExternalLogin` entity | Provider: Google, random subject ID                      |

---

## Coverage Gaps & Next Steps

| Area                         | Gap                                                                                                  | Priority |
| ---------------------------- | ---------------------------------------------------------------------------------------------------- | -------- |
| **Endpoint Tests**           | No `WebApplicationFactory` tests for `TenantEndpoints` or `AuthEndpoints` — needs full HTTP pipeline | High     |
| **Tenant Isolation Depth**   | No cross-tenant data visibility tests (write via tenant A, query via tenant B → zero results)        | High     |
| **Auth Flow E2E**            | No end-to-end register → login → refresh → logout integration test through HTTP endpoints            | High     |
| **MassTransit Integration**  | No end-to-end test with Testcontainers RabbitMQ — requires Testcontainers.RabbitMq package           | Medium   |
| **RequestLoggingMiddleware** | Not directly tested — logs via Serilog, lower value without log sink assertions                      | Low      |
