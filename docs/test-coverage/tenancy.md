# Tenancy Module Test Coverage

← [Test Coverage](README.md)

> Tests for tenant registration, lookup, caching, and status lifecycle.

---

## `TenantStatusTests` (7 tests)

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

## `TenantTests` (3 tests)

Tests the `Tenant` entity structure, its inheritance from `AggregateRoot`, and the `TenantBranding` navigation property.

| Test                                           | What It Verifies                                                      | Expected Outcome                                                                     |
| ---------------------------------------------- | --------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `Tenant_InheritsAggregateRoot_HasDomainEvents` | Tenant inherits domain event tracking from `AggregateRoot`            | `DomainEvents` collection exists and is empty                                        |
| `Tenant_NewInstance_HasExpectedDefaults`       | Factory-created tenant has correct property values and null optionals | Name, subdomain, status match; `ProvisionedAt`, `DeactivatedAt`, `Branding` are null |
| `Tenant_WithBranding_NavigationPropertySet`    | One-to-one branding association works correctly                       | `Branding` is not null, `TenantId` matches, colors populated                         |

**Why:** Tenant is the most accessed entity in the system — resolved on every inbound request via subdomain lookup. Validating its shape and relationships ensures the tenant resolution middleware, caching layer, and EF Core configuration will map correctly.

---

## `TenantServiceTests` (10 tests)

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

## `MemoryTenantCacheTests` (5 tests)

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

## `PlatformAdminServiceTests` (15 tests)

Tests `PlatformAdminService`, the application service for platform-wide tenant administration (list, get, suspend, reactivate).

| Test                                                          | What It Verifies                                                    | Expected Outcome                                                |
| ------------------------------------------------------------- | ------------------------------------------------------------------- | --------------------------------------------------------------- |
| `GetTenantsAsync_ReturnsPaginatedList`                        | Returns correct items count and pagination metadata                 | 2 items, `HasMore` false, `NextCursor` null                    |
| `GetTenantsAsync_WithMoreResults_ReturnsNextCursor`           | Cursor is set to last item ID when more results exist               | `HasMore` true, cursor matches last tenant ID                  |
| `GetTenantsAsync_WithStatusFilter_PassesFilterToRepository`   | Status filter is forwarded to repository                            | Repository called with `"Active"` status                       |
| `GetTenantsAsync_WithSearchFilter_PassesSearchToRepository`   | Search filter is forwarded to repository                            | Repository called with `"acme"` search                         |
| `GetTenantByIdAsync_ExistingTenant_ReturnsTenantResponse`     | Successful lookup maps entity to DTO                                | Response matches tenant entity                                  |
| `GetTenantByIdAsync_NonExistentId_ThrowsTenantNotFound`       | Missing tenant throws correct error                                 | Throws `AppError` with `TENANT_NOT_FOUND`                      |
| `SuspendTenantAsync_ActiveTenant_SuspendsSuccessfully`        | Active tenant transitions to Suspended                              | Status is `Suspended`, `DeactivatedAt` set, UoW saved          |
| `SuspendTenantAsync_NonExistentTenant_ThrowsTenantNotFound`   | Missing tenant throws correct error                                 | Throws `AppError` with `TENANT_NOT_FOUND`                      |
| `SuspendTenantAsync_AlreadySuspendedTenant_ThrowsInvalidRequest` | Cannot suspend an already suspended tenant                       | Throws `AppError` with `INVALID_REQUEST`                       |
| `SuspendTenantAsync_ProvisioningTenant_ThrowsInvalidRequest`  | Cannot suspend a provisioning tenant                                | Throws `AppError` with `INVALID_REQUEST`                       |
| `ReactivateTenantAsync_SuspendedTenant_ReactivatesSuccessfully` | Suspended tenant transitions to Active                            | Status is `Active`, `DeactivatedAt` cleared, UoW saved         |
| `ReactivateTenantAsync_NonExistentTenant_ThrowsTenantNotFound`| Missing tenant throws correct error                                 | Throws `AppError` with `TENANT_NOT_FOUND`                      |
| `ReactivateTenantAsync_ActiveTenant_ThrowsInvalidRequest`     | Cannot reactivate an already active tenant                          | Throws `AppError` with `INVALID_REQUEST`                       |
| `GetTenantByIdAsync_WithBranding_MapsBrandingToResponse`      | Branding navigation property is mapped to response DTO              | `Branding` not null, colors match                              |
| `GetTenantByIdAsync_WithNullBranding_ReturnsNullBranding`     | Missing branding returns null, not an error                         | `Branding` is null                                             |

**Why:** Platform admin is the only way to manage tenants system-wide (suspend/reactivate). If suspension logic is wrong, a suspended tenant could continue operating (security risk) or an active tenant could be incorrectly blocked. The state transition guards prevent invalid operations (e.g., suspending a provisioning tenant).
