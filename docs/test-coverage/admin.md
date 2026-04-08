# Admin Module Test Coverage

← [Test Coverage](README.md)

> Tests for company settings, audit logging, domain event handlers, and tenant provisioning.

---

## `AdminSettingsServiceTests` (8 tests)

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

## `AuditLogServiceTests` (5 tests)

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

## `AuditEventHandlerTests` (6 tests)

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

## `TenantProvisionedHandlerTests` (3 tests)

Tests the `TenantProvisionedHandler` that seeds default `CompanySettings` when a new tenant is provisioned.

| Test                                           | What It Verifies                                                                    | Expected Outcome                                       |
| ---------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------------------------------------ |
| `Handle_NewTenant_SeedsDefaultCompanySettings` | Default settings created with correct `TenantId` and default values                 | Settings entity added to repository                    |
| `Handle_NewTenant_CreatesAuditLogEntry`        | `TenantProvisioned` audit log entry created after seeding                           | `LogAsync` called with `AuditAction.TenantProvisioned` |
| `Handle_NewTenant_DefaultScreeningCriteria`    | Default screening criteria (Skills 40%, Experience 30%, Education 15%, Quality 15%) | JSON contains all 4 criteria with correct weights      |

**Why:** `TenantProvisionedHandler` is the only way `CompanySettings` gets created. If seeding fails or defaults are wrong, the Admin settings endpoints will return 404 for every new tenant.

---

## `AuditConstantsTests` (6 tests)

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

## `UpdateCompanySettingsRequestValidatorTests` (11 tests)

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

## `DashboardServiceTests` (3 tests)

Tests `DashboardService`, the service that aggregates pipeline statistics from Recruitment, Screening, and Matching modules via cross-module readers.

| Test                                                 | What It Verifies                                                             | Expected Outcome                                        |
| ---------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------- |
| `GetStatsAsync_ReturnsAggregatedStats`               | All stats from 3 modules are correctly mapped into the response              | All recruitment, screening, and matching fields match    |
| `GetStatsAsync_WithNullAverageScore_ReturnsNullScore`| Empty tenant with no screenings returns null average score and zero counts   | `AverageScore` is null, all counts are 0                |
| `GetStatsAsync_CallsAllReaders`                      | All three cross-module readers are invoked exactly once                      | Each reader received exactly 1 call                     |

**Why:** The dashboard endpoint is the primary overview for agency admins. It aggregates data from 3 independent modules via SharedKernel reader interfaces. If any reader isn't called or its data is mis-mapped, the dashboard shows incorrect pipeline statistics.
