# Test Coverage Working Document

> Living document tracking all implemented tests, their coverage, rationale, and expected outcomes.

## Test Summary

| Project                   | Tests  | Status              |
| ------------------------- | ------ | ------------------- |
| Jobsite.UnitTests         | 33     | ✅ All passing      |
| Jobsite.ArchitectureTests | 10     | ✅ All passing      |
| Jobsite.IntegrationTests  | 1      | ⬜ Placeholder only |
| **Total**                 | **44** |                     |

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

### Tenancy Module

#### `TenantStatusTests` (6 tests)

Tests the `TenantStatus` constants and the `IsValid()` validation method. Status values must match the PostgreSQL CHECK constraint `chk_tenants_status` exactly.

| Test                                   | What It Verifies                                             | Expected Outcome |
| -------------------------------------- | ------------------------------------------------------------ | ---------------- |
| `IsValid_Provisioning_ReturnsTrue`     | "Provisioning" is a valid status                             | Returns `true`   |
| `IsValid_Active_ReturnsTrue`           | "Active" is a valid status                                   | Returns `true`   |
| `IsValid_Suspended_ReturnsTrue`        | "Suspended" is a valid status                                | Returns `true`   |
| `IsValid_Deactivated_ReturnsTrue`      | "Deactivated" is a valid status                              | Returns `true`   |
| `IsValid_UnknownStatus_ReturnsFalse`   | "Deleted" is rejected as an invalid status                   | Returns `false`  |
| `IsValid_LowercaseStatus_ReturnsFalse` | "active" (lowercase) is rejected — PascalCase per convention | Returns `false`  |

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

#### `TenantServiceTests` (8 tests)

Tests `TenantService`, the application service for tenant registration and lookup. Uses NSubstitute to mock `ITenantRepository` and `IUnitOfWork`.

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

**Why:** `TenantService` is the entry point for all tenant operations. The uniqueness checks (subdomain/name) prevent data integrity violations before they hit the database. Subdomain lowercasing is critical because DNS labels are case-insensitive — inconsistent casing would cause cache misses and lookup failures in the tenant resolution middleware. The mock-based approach validates business logic in isolation without needing a real database.

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

### `NamingConventionTests` (3 tests)

Enforces coding standards from `docs/conventions/DOTNET_CONVENTIONS.md`.

| Test                                           | What It Verifies                                                                             | Expected Outcome         |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------- | ------------------------ |
| `ConcreteClasses_ShouldBeSealed`               | All concrete classes in Tenancy.Domain are `sealed`                                          | No violating types found |
| `InfrastructureConcreteClasses_ShouldBeSealed` | All concrete classes in Tenancy.Infrastructure are `sealed` (excluding EF migration classes) | No violating types found |
| `Interfaces_ShouldStartWithI`                  | All interfaces in Tenancy.Domain follow the `I` prefix convention                            | No violating types found |

**Why:** The project mandate is `sealed class` on all concrete classes unless inheritance is explicitly needed. Unsealed classes invite accidental inheritance, break assumptions about type identity, and prevent certain runtime optimizations. EF Core migration classes are excluded because they're auto-generated and inherit from `Migration`. The `I` prefix test catches unconventional interface names that would confuse developers.

---

### `ModuleIsolationTests` (2 tests)

Enforces that modules do not cross-reference each other — modules communicate only through SharedKernel domain events.

| Test                                                    | What It Verifies                                                                                            | Expected Outcome         |
| ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ------------------------ |
| `TenancyDomain_ShouldNotReference_OtherModules`         | Tenancy.Domain has no dependency on Auth, Profiles, Recruitment, Screening, HRWorkflows, Matching, or Admin | No violating types found |
| `TenancyInfrastructure_ShouldNotReference_OtherModules` | Tenancy.Infrastructure has no dependency on any other module                                                | No violating types found |

**Why:** Cross-module references are the primary way a modular monolith degrades into a big ball of mud. If Tenancy references Recruitment directly, extracting either into a separate service later becomes impossible. These tests enforce that inter-module communication goes through SharedKernel events only, keeping module boundaries clean.

---

## Test Data Factory (`TestData.cs`)

Centralized factory methods for test object creation. Avoids inline object construction per `docs/conventions/TESTING_STANDARDS.md`.

| Method                          | Creates                     | Default Values                                       |
| ------------------------------- | --------------------------- | ---------------------------------------------------- |
| `CreateTenant()`                | `Tenant` entity             | Name: "Acme Corp", Subdomain: "acme", Status: Active |
| `CreateTenantBranding()`        | `TenantBranding` entity     | Primary: #1A73E8, Tagline: "Test tagline"            |
| `CreateRegisterTenantRequest()` | `RegisterTenantRequest` DTO | Name: "Acme Corp", Subdomain: "acme"                 |

All methods accept optional overrides for customization in specific test scenarios.

---

## Coverage Gaps & Next Steps

| Area                   | Gap                                                                                                                     | Priority    |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------- | ----------- |
| **Integration Tests**  | No real database tests yet — `IntegrationFixture` with Testcontainers needed for `TenantRepository`, `CatalogDbContext` | High        |
| **Endpoint Tests**     | No `WebApplicationFactory` tests for `TenantEndpoints` (POST/GET)                                                       | High        |
| **Middleware Tests**   | `TenantResolutionMiddleware`, `AppErrorMiddleware`, `CorrelationIdMiddleware` untested                                  | Medium      |
| **Auth Module**        | Not yet implemented — will need JWT issuance, refresh token, replay detection tests                                     | Next module |
| **Integration Events** | Serialization contract tests for C# ↔ Python boundary (e.g., `ApplicationSubmittedEvent`)                               | Medium      |
| **SharedKernel**       | `IUnitOfWork` only tested indirectly via `TenantServiceTests` mock verification                                         | Low         |
