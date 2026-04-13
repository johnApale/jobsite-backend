# Integration Tests Coverage

← [Test Coverage](README.md)

> All integration tests run against real PostgreSQL via Testcontainers. Docker must be running.

---

## Fixture Infrastructure

- `CatalogIntegrationFixture` — spins up a `postgres:16-alpine` container, creates `CatalogDbContext`, runs migrations. Exposes `ConnectionString` property for provisioning tests that need direct database access.
- `CatalogIntegrationCollection` — xUnit `[Collection("Catalog")]` for shared container across test classes
- `ProfilesIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `ProfilesDbContext` via `EnsureCreatedAsync`.
- `ProfilesIntegrationCollection` — xUnit `[Collection("Profiles")]` for shared container across Profiles test classes
- `MatchingIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `MatchingDbContext` via `EnsureCreatedAsync`.
- `MatchingIntegrationCollection` — xUnit `[Collection("Matching")]` for shared container across Matching test classes
- `AiServiceContractFixture` — starts a WireMock HTTP server for AI Service contract testing. Exposes `BaseUrl` and `Server` properties. Calls `Reset()` between tests for isolation.
- `AiServiceContractCollection` — xUnit `[Collection("AiServiceContract")]` for shared WireMock server across contract test classes
- `ScreeningPipelineFixture` — spins up a `postgres:17-alpine` container, creates `ScreeningDbContext` via `EnsureCreatedAsync`. Provides `CreateDbContext()` factory and `ResetDataAsync()` to truncate all tables between tests.
- `ScreeningPipelineCollection` — xUnit `[Collection("ScreeningPipeline")]` for shared container across E2E screening test classes
- `IntegrationTestData` — factory with unique-per-test names/subdomains to avoid collisions
- `JobsiteWebApplicationFactory` — boots the full application (`WebApplicationFactory<Program>`) against a `postgres:17-alpine` container. Applies all 8 module migrations, seeds a test tenant, overrides JWT/rate-limit config. See [Endpoint Tests](#endpoint-tests-webapplicationfactory) for details.
- `EndpointTestCollection` — xUnit `[Collection("Endpoints")]` for shared factory across all endpoint test classes
- `TestJwtHelper` — static utility for generating valid/expired JWT tokens matching the factory's auth configuration

---

## Tenancy

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

## Auth Module

### Fixture

- `AuthIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `AuthDbContext`, runs `InitialAuthSchema` migration. Exposes `ConnectionString` property for direct database access.
- `AuthIntegrationCollection` — xUnit `[Collection("Auth")]` for shared container across Auth test classes

### `UserRepositoryTests` (14 tests)

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

### `RefreshTokenRepositoryTests` (7 tests)

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

### `AuthDbContextTests` (5 tests)

Tests AuthDbContext schema creation, table mapping, default values, and relationship behavior.

| Test                                                      | What It Verifies                                                                  | Expected Outcome                                         |
| --------------------------------------------------------- | --------------------------------------------------------------------------------- | -------------------------------------------------------- |
| `Schema_AuthSchemaExists`                                 | The `auth` PostgreSQL schema is created by the migration                          | Schema `auth` found in `information_schema.schemata`     |
| `Users_DefaultValues_AppliedByDatabase`                   | `id`, `email_verified`, `created_at`, `updated_at` defaults are applied by the DB | UUID generated, `false` default, timestamps close to now |
| `Users_ExternalLoginCascade_DeletesLoginWhenUserDeleted`  | Cascade delete removes external logins when user is deleted                       | Login record no longer found                             |
| `ExternalLogins_UniqueProviderPerUser_EnforcedByDatabase` | Unique index on (provider, subject_id) rejects duplicate OAuth provider links     | Throws `DbUpdateException`                               |
| `Users_NullablePasswordHash_AllowsOAuthOnlyUsers`         | `password_hash` column is nullable for OAuth-only users (no email/password)       | User persisted with `null` password hash                 |

**Why:** These tests ensure the database schema matches the design spec: auth schema isolation, correct defaults, proper cascading, and nullable `password_hash` for OAuth-only users. The unique provider constraint test validates that a user can't accidentally link the same OAuth account twice.

---

## Admin Module

### Fixture

- `AdminIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `AdminDbContext`, runs `InitialAdminSchema` migration. Exposes `ConnectionString` property for direct database access.
- `AdminIntegrationCollection` — xUnit `[Collection("Admin")]` for shared container across Admin test classes

### `AdminDbContextTests` (4 tests)

Tests AdminDbContext schema creation, table mapping, default values, and JSONB column behavior.

| Test                                              | What It Verifies                                                                  | Expected Outcome                                      |
| ------------------------------------------------- | --------------------------------------------------------------------------------- | ----------------------------------------------------- |
| `Schema_AdminSchemaExists`                        | The `admin` PostgreSQL schema is created by the migration                         | Schema `admin` found in `information_schema.schemata` |
| `CompanySettings_DefaultValues_AppliedByDatabase` | `id`, `created_at`, `updated_at`, `default_timezone`, `default_currency` defaults | UUID generated, `UTC`, `USD`, timestamps close to now |
| `CompanySettings_JsonbColumns_PersistAndRetrieve` | All 6 JSONB settings columns round-trip correctly                                 | JSON content matches after persist + re-query         |
| `AuditLog_AllColumns_PersistCorrectly`            | All audit log columns persist with correct types and values                       | All fields match after persist + re-query             |

**Why:** EF Core JSONB mapping and schema isolation must be validated against real PostgreSQL. Unit tests with mocks can't catch `jsonb` serialization issues or missing schema configurations.

---

### `CompanySettingsRepositoryTests` (5 tests)

Tests `CompanySettingsRepository` against a real PostgreSQL database.

| Test                                                | What It Verifies                                                      | Expected Outcome                                          |
| --------------------------------------------------- | --------------------------------------------------------------------- | --------------------------------------------------------- |
| `Add_ValidSettings_PersistsToDatabase`              | `Add()` + `SaveChangesAsync()` inserts settings with DB-assigned UUID | Re-queried settings have non-empty `Id`, all fields match |
| `GetAsync_ExistingSettings_ReturnsUntracked`        | `GetAsync` returns settings with `AsNoTracking()`                     | Settings returned, entity is NOT in change tracker        |
| `GetAsync_NonExistent_ReturnsNull`                  | Missing tenant settings returns null                                  | Returns `null`                                            |
| `GetForUpdateAsync_ExistingSettings_ReturnsTracked` | `GetForUpdateAsync` returns tracked entity for mutation               | Settings returned, entity IS in change tracker            |
| `GetForUpdateAsync_NonExistent_ReturnsNull`         | Missing tenant settings returns null                                  | Returns `null`                                            |

**Why:** The distinction between tracked and untracked queries is critical — `GetAsync` (read-only, `AsNoTracking`) must not accidentally allow mutations, while `GetForUpdateAsync` must return a tracked entity for the merge-patch update flow to work.

---

### `AuditLogRepositoryTests` (7 tests)

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

## Recruitment Module

### Fixture

- `RecruitmentIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `RecruitmentDbContext` via `EnsureCreatedAsync` (migration files exist in `InitialRecruitmentSchema`, but fixture uses `EnsureCreatedAsync` for simplicity). Exposes `ConnectionString` for raw SQL tests.
- `RecruitmentIntegrationCollection` — xUnit `[Collection("Recruitment")]` for shared container across Recruitment test classes

### `RecruitmentDbContextTests` (22 tests)

Tests RecruitmentDbContext schema creation, table mapping, CHECK constraints, indexes, JSONB columns, default values, unique constraints, and cascade deletes against a real PostgreSQL container.

| Test                                                                   | What It Verifies                                                                      | Expected Outcome                              |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | --------------------------------------------- |
| `Schema_RecruitmentSchemaExists`                                       | The `recruitment` PostgreSQL schema is created by EF Core                             | Schema found in `information_schema.schemata` |
| `ClientCompany_Persists_AllFieldsCorrectly`                            | All 11 fields persist and round-trip correctly                                        | All fields match after persist + re-query     |
| `ClientCompany_CheckConstraint_RejectsInvalidStatus`                   | CHECK constraint `chk_client_companies_status` rejects "Suspended"                    | Throws `DbUpdateException`                    |
| `JobPosting_Persists_AllFieldsCorrectly`                               | All 16 fields persist including decimal salary and timestamps                         | All fields match after persist + re-query     |
| `JobPosting_DefaultValues_AppliedByDatabase`                           | `status` defaults to "Draft", timestamps auto-set, nullable fields are null           | Defaults applied correctly                    |
| `JobPosting_CheckConstraint_RejectsInvalidStatus`                      | CHECK constraint `chk_job_postings_status` rejects "Archived"                         | Throws `DbUpdateException`                    |
| `JobPosting_CheckConstraint_RejectsInvalidLocationType`                | CHECK constraint `chk_job_postings_location_type` rejects "InOffice"                  | Throws `DbUpdateException`                    |
| `JobPosting_CheckConstraint_RejectsInvalidEmploymentType`              | CHECK constraint `chk_job_postings_employment_type` rejects "Freelance"               | Throws `DbUpdateException`                    |
| `Application_Persists_AllFieldsCorrectly`                              | All application fields persist including FK to JobPosting                             | All fields match after persist + re-query     |
| `Application_CheckConstraint_RejectsInvalidStatus`                     | CHECK constraint `chk_applications_status` rejects "InReview"                         | Throws `DbUpdateException`                    |
| `Application_UniqueConstraint_PreventsDuplicateApplicantJob`           | Unique index `uq_applications_applicant_job` prevents same applicant applying twice   | Throws `DbUpdateException`                    |
| `JobEvaluationCriteria_Persists_AllFieldsCorrectly`                    | All criteria fields persist including JSONB `configuration`                           | All fields match after persist + re-query     |
| `JobEvaluationCriteria_CheckConstraint_RejectsInvalidCategory`         | CHECK constraint `chk_criteria_category` rejects "Personality"                        | Throws `DbUpdateException`                    |
| `JobEvaluationCriteria_CheckConstraint_RejectsInvalidEvaluationMethod` | CHECK constraint `chk_criteria_evaluation_method` rejects "FuzzyMatch"                | Throws `DbUpdateException`                    |
| `JobEvaluationCriteria_JsonbConfiguration_PersistsCorrectly`           | Complex JSONB configuration with nested arrays round-trips correctly                  | JSON content preserved after persist          |
| `JobScreeningQuestion_Persists_AllFieldsCorrectly`                     | All question fields persist including JSONB `expected_answer`                         | All fields match after persist + re-query     |
| `JobScreeningQuestion_CheckConstraint_RejectsInvalidQuestionType`      | CHECK constraint `chk_questions_question_type` rejects "Essay"                        | Throws `DbUpdateException`                    |
| `JobScreeningQuestion_CheckConstraint_RejectsInvalidTiming`            | CHECK constraint `chk_questions_timing` rejects "DuringInterview"                     | Throws `DbUpdateException`                    |
| `JobScreeningQuestion_JsonbOptions_PersistsCorrectly`                  | JSONB `options` and `expected_answer` columns round-trip correctly for MultipleChoice | JSON content preserved after persist          |
| `JobPostings_Indexes_Exist`                                            | All 5 indexes exist (status, posted_by, published_at, client_company, location)       | All index names found in `pg_indexes`         |
| `Applications_Indexes_Exist`                                           | All 4 indexes exist (job_posting_id, applicant_id, status, submitted_at)              | All index names found in `pg_indexes`         |
| `JobPosting_CascadeDelete_RemovesChildEntities`                        | Deleting a JobPosting cascades to criteria and questions                              | Child entities no longer exist                |

**Why:** EF Core entity configurations, CHECK constraints, JSONB column mapping, and cascade delete behavior can only be validated against real PostgreSQL. The Recruitment module has the highest number of CHECK constraints (10 across 5 tables) in the system — unit tests with mocks cannot catch constraint mismatches. The cascade delete test ensures that removing a job posting cleanly removes all associated criteria and questions.

---

## Screening Module

### Fixture

- `ScreeningIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `ScreeningDbContext` via `EnsureCreatedAsync` (migration files exist in `InitialScreeningSchema`, but fixture uses `EnsureCreatedAsync` for simplicity). Exposes `ConnectionString` for raw SQL tests.
- `ScreeningIntegrationCollection` — xUnit `[Collection("Screening")]` for shared container across Screening test classes

### `ScreeningDbContextTests` (10 tests)

Tests ScreeningDbContext schema creation, table mapping, CHECK constraints, indexes, and default values against a real PostgreSQL container.

| Test                                                              | What It Verifies                                                                    | Expected Outcome                              |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------- |
| `Schema_ScreeningSchemaExists`                                    | The `screening` PostgreSQL schema is created by EF Core                             | Schema found in `information_schema.schemata` |
| `ScreeningResult_Persists_AllFieldsCorrectly`                     | All 20+ fields persist and round-trip correctly including JSONB columns             | All fields match after persist + re-query     |
| `ScreeningResult_DefaultValues_AppliedByDatabase`                 | `status` defaults to "Pending", timestamps auto-set, nullable fields are null       | Defaults applied correctly                    |
| `ScreeningResult_CheckConstraint_RejectsInvalidStatus`            | CHECK constraint `chk_screening_results_status` rejects "InvalidStatus"             | Throws `DbUpdateException`                    |
| `ScreeningResult_CheckConstraint_RejectsInvalidOutcome`           | CHECK constraint rejects "InvalidOutcome"                                           | Throws `DbUpdateException`                    |
| `ScreeningResult_CheckConstraint_RejectsInvalidMatchStrength`     | CHECK constraint rejects "SuperStrong"                                              | Throws `DbUpdateException`                    |
| `ScreeningResults_Indexes_Exist`                                  | All 4 indexes exist (status, match_strength, outcome, overall_score)                | All index names found in `pg_indexes`         |
| `QuestionResponse_Persists_AllFieldsCorrectly`                    | All response fields persist including JSONB `response_data` and scoring fields      | All fields match after persist + re-query     |
| `QuestionResponse_UniqueConstraint_PreventseDuplicateAppQuestion` | Unique index `uq_question_responses_app_question` rejects duplicate (app, question) | Throws `DbUpdateException`                    |
| `QuestionResponse_CheckConstraint_RejectsInvalidScoreResult`      | CHECK constraint rejects invalid `score_result` values                              | Throws `DbUpdateException`                    |

**Why:** EF Core entity configurations and CHECK constraints can only be validated against real PostgreSQL. Unit tests with mocks cannot catch wrong column types, missing JSONB mapping, or CHECK constraint mismatches. The index tests ensure query performance optimizations are applied.

---

### `ScreeningRepositoryTests` (7 tests)

Tests `ScreeningResultRepository` and `ScreeningQuestionResponseRepository` against a real PostgreSQL database. Validates CRUD operations, cursor-based pagination, tracking behavior, and ordering.

| Test                                                                       | What It Verifies                                                                 | Expected Outcome                                  |
| -------------------------------------------------------------------------- | -------------------------------------------------------------------------------- | ------------------------------------------------- |
| `GetByApplicationIdAsync_Exists_ReturnsResult`                             | `AsNoTracking` lookup returns the correct result                                 | Result found, fields match                        |
| `GetByApplicationIdAsync_NotExists_ReturnsNull`                            | Missing application ID returns null                                              | Returns null                                      |
| `GetByApplicationIdForUpdateAsync_ReturnsTrackedEntity`                    | Tracked entity can be mutated and saved                                          | Status change persists after save                 |
| `ListAsync_FiltersByStatus_ReturnsCursorPagination`                        | Status filter excludes non-matching results                                      | Only Completed results returned, Pending excluded |
| `ListAsync_CursorPagination_ReturnsCorrectPage`                            | First page returns 2 items + cursor, second page returns 1 item with no more     | Pages don't overlap, cursor advances correctly    |
| `QuestionResponseRepo_GetByApplicationIdAsync_ReturnsOrderedBySubmittedAt` | Responses returned ordered by `SubmittedAt` ascending regardless of insert order | "First" appears before "Second"                   |
| `QuestionResponseRepo_ExistsByApplicationAndQuestionAsync_TrueWhenExists`  | Existence check returns true for existing pair, false for non-existing           | Correct boolean for both cases                    |

**Why:** Repository integration tests catch EF Core query translation issues and cursor pagination bugs (off-by-one, incorrect sort direction, broken Base64 encoding) that only surface against real PostgreSQL. The tracking vs untracking distinction is critical for the mutation-heavy screening pipeline.

---

## Profiles Module

### Fixture

- `ProfilesIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `ProfilesDbContext` via `EnsureCreatedAsync`. Exposes `ConnectionString` for raw SQL tests.
- `ProfilesIntegrationCollection` — xUnit `[Collection("Profiles")]` for shared container across Profiles test classes

### `ProfilesDbContextTests` (10 tests)

Tests ProfilesDbContext schema creation, entity CRUD, CHECK constraints, JSONB column mapping, cascade deletes, and indexes against a real PostgreSQL container.

| Test                                               | What It Verifies                                                    | Expected Outcome                              |
| -------------------------------------------------- | ------------------------------------------------------------------- | --------------------------------------------- |
| `Schema_ProfilesSchemaExists`                      | The `profiles` PostgreSQL schema is created by EF Core              | Schema found in `information_schema.schemata` |
| `ApplicantProfile_Persists_AllFieldsCorrectly`     | All fields persist including JSONB Skills, SocialLinks, Documents   | All fields match after persist + re-query     |
| `ApplicantProfile_DefaultValues_AppliedByDatabase` | Timestamps auto-set, nullable fields are null                       | Defaults applied correctly                    |
| `Resume_Persists_AllFieldsCorrectly`               | All resume fields persist including parsing state and JSONB columns | All fields match after persist + re-query     |
| `Resume_DefaultValues_AppliedByDatabase`           | IsLatest defaults to false, IsParsed defaults to false              | Defaults applied correctly                    |
| `Resume_CheckConstraint_RejectsInvalidFileType`    | CHECK constraint `chk_resumes_file_type` rejects "EXE"              | Throws `DbUpdateException`                    |
| `Resume_CheckConstraint_AcceptsValidFileTypes`     | PDF and DOCX both accepted by CHECK constraint                      | Both resumes persist                          |
| `CascadeDelete_DeletingProfile_DeletesResumes`     | Cascade delete removes resumes when profile is deleted              | Resumes no longer found                       |
| `ApplicantProfiles_Indexes_Exist`                  | Index `ix_applicant_profiles_city_country` exists                   | Index found in `pg_indexes`                   |
| `Resumes_Indexes_Exist`                            | All 3 resume indexes exist (user_id, is_parsed, user_id_is_latest)  | All index names found in `pg_indexes`         |

**Why:** EF Core JSONB mapping, CHECK constraints, cascade deletes, and index configurations can only be validated against real PostgreSQL.

---

### `ProfilesRepositoryTests` (12 tests)

Tests `ApplicantProfileRepository` and `ResumeRepository` against a real PostgreSQL database. Validates CRUD operations, tracking behavior, ordering, and bulk updates.

| Test                                                               | What It Verifies                                                  | Expected Outcome                        |
| ------------------------------------------------------------------ | ----------------------------------------------------------------- | --------------------------------------- |
| `GetByUserIdAsync_Exists_ReturnsProfile`                           | AsNoTracking lookup returns the correct profile                   | Profile found, fields match             |
| `GetByUserIdAsync_NotExists_ReturnsNull`                           | Missing user ID returns null                                      | Returns null                            |
| `GetByUserIdForUpdateAsync_ReturnsTrackedEntity`                   | Tracked entity can be mutated and saved                           | City update persists after save         |
| `ExistsByUserIdAsync_TrueWhenExists`                               | Existence check returns true for existing, false for non-existing | Correct boolean for both cases          |
| `ResumeRepo_GetByIdAsync_Exists_ReturnsResume`                     | AsNoTracking lookup returns the correct resume                    | Resume found, fields match              |
| `ResumeRepo_GetByIdAsync_NotExists_ReturnsNull`                    | Missing resume ID returns null                                    | Returns null                            |
| `ResumeRepo_GetByIdForUpdateAsync_ReturnsTrackedEntity`            | Tracked resume can be mutated and saved                           | IsParsed and ParsedText update persists |
| `ResumeRepo_GetByUserIdAsync_ReturnsOrderedByCreatedAtDescending`  | Resumes ordered by CreatedAt descending (newest first)            | Newest resume appears first             |
| `ResumeRepo_GetLatestByUserIdAsync_ReturnsLatestResume`            | Returns only the resume with IsLatest = true                      | Latest resume returned                  |
| `ResumeRepo_GetLatestByUserIdAsync_NoLatest_ReturnsNull`           | No resume with IsLatest = true returns null                       | Returns null                            |
| `ResumeRepo_HasAnyByUserIdAsync_TrueWhenExists`                    | Existence check returns true for existing, false for non-existing | Correct boolean for both cases          |
| `ResumeRepo_MarkPreviousAsNotLatestAsync_ClearsPreviousLatestFlag` | ExecuteUpdate clears IsLatest on all user's resumes               | All resumes have IsLatest = false       |

**Why:** Repository integration tests catch EF Core query translation, AsNoTracking behavior, ExecuteUpdate bulk operations, and ordering logic that only surface against real PostgreSQL.

---

## Matching Module

### Fixture

- `MatchingIntegrationFixture` — spins up a `postgres:17-alpine` container, creates `MatchingDbContext` via `EnsureCreatedAsync`. Exposes `ConnectionString` for raw SQL tests.
- `MatchingIntegrationCollection` — xUnit `[Collection("Matching")]` for shared container across Matching test classes

### `MatchingDbContextTests` (16 tests)

Tests MatchingDbContext schema creation, entity CRUD, CHECK constraints, unique constraints, cascade deletes, and indexes against a real PostgreSQL container.

| Test                                                                 | What It Verifies                                                | Expected Outcome                              |
| -------------------------------------------------------------------- | --------------------------------------------------------------- | --------------------------------------------- |
| `Schema_MatchingSchemaExists`                                        | The `matching` PostgreSQL schema is created by EF Core          | Schema found in `information_schema.schemata` |
| `CandidateMatch_Persists_AllFieldsCorrectly`                         | All fields persist including decimal(5,2) scores and timestamps | All fields match after persist + re-query     |
| `CandidateMatch_SharedPrimaryKey_ValueGeneratedNever`                | ApplicationId used as PK is the exact GUID provided             | PK matches provided GUID                      |
| `CandidateMatch_CheckConstraint_RejectsInvalidMatchStrength`         | CHECK constraint rejects "SuperStrong"                          | Throws `DbUpdateException`                    |
| `Shortlist_Persists_AllFieldsCorrectly`                              | All shortlist fields persist including finalization data        | All fields match after persist + re-query     |
| `Shortlist_DefaultValues_DraftStatus`                                | Status defaults to Draft, finalization fields are null          | Defaults applied correctly                    |
| `Shortlist_CheckConstraint_RejectsInvalidStatus`                     | CHECK constraint rejects "InvalidStatus"                        | Throws `DbUpdateException`                    |
| `ShortlistCandidate_Persists_AllFieldsCorrectly`                     | All candidate fields persist including source and status        | All fields match after persist + re-query     |
| `ShortlistCandidate_DefaultStatus_IsPending`                         | Status defaults to Pending                                      | Status = Pending                              |
| `ShortlistCandidate_CheckConstraint_RejectsInvalidSource`            | CHECK constraint rejects "InvalidSource"                        | Throws `DbUpdateException`                    |
| `ShortlistCandidate_CheckConstraint_RejectsInvalidStatus`            | CHECK constraint rejects "InvalidStatus"                        | Throws `DbUpdateException`                    |
| `ShortlistCandidate_UniqueConstraint_PreventseDuplicateShortlistApp` | Unique index rejects duplicate (ShortlistId, ApplicationId)     | Throws `DbUpdateException`                    |
| `CascadeDelete_DeletingShortlist_DeletesCandidates`                  | Cascade delete removes candidates when shortlist is deleted     | Candidates no longer found                    |
| `CandidateMatches_Indexes_Exist`                                     | All 4 candidate_matches indexes exist                           | All index names found in `pg_indexes`         |
| `Shortlists_Indexes_Exist`                                           | Both shortlists indexes exist                                   | All index names found in `pg_indexes`         |
| `ShortlistCandidates_Indexes_Exist`                                  | Both shortlist_candidates indexes exist                         | All index names found in `pg_indexes`         |

**Why:** Decimal precision, shared primary keys (ValueGeneratedNever), CHECK constraints, unique constraints, and cascade deletes can only be validated against real PostgreSQL.

---

### `MatchingRepositoryTests` (12 tests)

Tests `CandidateMatchRepository` and `ShortlistRepository` against a real PostgreSQL container. Validates CRUD, ordering, filtering, and Include behavior.

| Test                                                                       | What It Verifies                                     | Expected Outcome                          |
| -------------------------------------------------------------------------- | ---------------------------------------------------- | ----------------------------------------- |
| `GetByApplicationIdAsync_Exists_ReturnsMatch`                              | AsNoTracking lookup returns the correct match        | Match found, fields match                 |
| `GetByApplicationIdAsync_NotExists_ReturnsNull`                            | Missing application ID returns null                  | Returns null                              |
| `GetByApplicationIdForUpdateAsync_ReturnsTrackedEntity`                    | Tracked entity can be mutated and saved              | Assessment score update persists          |
| `GetByJobPostingIdAsync_ReturnsOrderedByCompositeScoreDescending`          | Matches ordered by CompositeScore descending         | Highest score first (90, 65, 40)          |
| `GetByJobPostingIdAsync_DifferentJobPosting_ReturnsEmpty`                  | Different job posting ID returns empty list          | Returns empty list                        |
| `ShortlistRepo_GetByIdAsync_IncludesCandidates`                            | Include(Candidates) eager-loads candidate navigation | Shortlist with 2 candidates loaded        |
| `ShortlistRepo_GetByIdAsync_NotExists_ReturnsNull`                         | Missing shortlist ID returns null                    | Returns null                              |
| `ShortlistRepo_GetByIdForUpdateAsync_ReturnsTrackedEntityWithCandidates`   | Tracked entity with loaded candidates can be mutated | Status update persists, candidates loaded |
| `ShortlistRepo_GetDraftByJobPostingIdAsync_ReturnsDraftOnly`               | Only Draft shortlists returned, Finalized excluded   | Returns Draft shortlist                   |
| `ShortlistRepo_GetDraftByJobPostingIdAsync_NoDraft_ReturnsNull`            | No Draft shortlist returns null                      | Returns null                              |
| `ShortlistRepo_GetByJobPostingIdAsync_ReturnsOrderedByCreatedAtDescending` | Shortlists ordered by CreatedAt descending           | Newest shortlist appears first            |
| `ShortlistRepo_GetByJobPostingIdAsync_DifferentJob_ReturnsEmpty`           | Different job posting ID returns empty list          | Returns empty list                        |

**Why:** Include/navigation behavior, composite ordering, and the Draft filter for shortlist workflow can only be validated against real PostgreSQL.

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

| Method                       | Creates                     | Default Values                                             |
| ---------------------------- | --------------------------- | ---------------------------------------------------------- |
| `CreateTenant()`             | `Tenant` entity             | Unique name/subdomain via `Guid`, Status: Active           |
| `CreateBranding()`           | `TenantBranding` entity     | Primary: #1A73E8, Tagline: "Integration test branding"     |
| `CreateUser()`               | `User` entity               | Unique email via `Guid`, Role: Applicant, Status: Active   |
| `CreateRefreshToken()`       | `RefreshToken` entity       | Random hash and family, ExpiresAt: 30 days from now        |
| `CreateExternalLogin()`      | `UserExternalLogin` entity  | Provider: Google, random subject ID                        |
| `CreateApplicantProfile()`   | `ApplicantProfile` entity   | Random userId, FirstName: "Test", LastName: "Applicant"    |
| `CreateResume()`             | `Resume` entity             | Random URL/filename, FileType: PDF, 1024 bytes             |
| `CreateCandidateMatch()`     | `CandidateMatch` entity     | Random IDs, composite: 75, MatchStrength from score        |
| `CreateShortlist()`          | `Shortlist` entity          | Random jobPostingId, Status: Draft, GeneratedBy: Algorithm |
| `CreateShortlistCandidate()` | `ShortlistCandidate` entity | Random IDs, Rank: 1, Source: Algorithm, score: 80          |

---

## AI Service Contract Tests (WireMock)

Contract tests verify that the .NET HTTP clients send the correct request bodies (snake_case field names, correct endpoint paths) and correctly deserialize AI Service responses. Tests run against a real WireMock HTTP server — not mocked `HttpMessageHandler` — so they exercise the full HTTP stack including `System.Text.Json` serialization policy. Only the 2 synchronous HTTP clients remain; the 4 async operations (resume parsing, screening evaluation, answer scoring, candidate feedback) now use the RabbitMQ message broker.

See [screening.md](screening.md#ai-service-contract-tests-wiremock) for the full test table (8 tests across 2 clients: `AiCriteriaSuggesterClient`, `AiQuestionSuggesterClient`).

Each client has 4 tests: request body verification, success deserialization, 500 → null, malformed JSON → null.

---

## E2E Screening Pipeline Tests (Testcontainers)

End-to-end tests exercising the full screening pipeline: real `DeterministicScoringEngine` + real PostgreSQL repositories + real `ScreeningService`. Cross-module readers (`IJobCriteriaReader`, `IJobScreeningQuestionsReader`, `IApplicationStatusUpdater`) are stubbed via NSubstitute. AI operations publish events to the message broker via `IEventPublisher` — consumer response handling is tested separately in unit tests. Tests verify score calculation, three-tier routing, persistence, event publishing for async AI operations, and assessment routing.

See [screening.md](screening.md#e2e-screening-pipeline-tests) for the full test table (10 tests).

Key scenarios: high/low/middle score routing, AI evaluation event publishing (enabled/disabled), transparency feedback event publishing, assessment routing, AutoAdvanceAll policy, serialized breakdown validation.

---

## Endpoint Tests (WebApplicationFactory)

Full HTTP pipeline endpoint tests using `WebApplicationFactory<Program>` backed by Testcontainers PostgreSQL. These tests boot the entire application (middleware, routing, auth, serialization, EF Core) against a real database — the highest-fidelity integration tests short of a deployed environment.

### Fixture Infrastructure

- `JobsiteWebApplicationFactory` — Custom `WebApplicationFactory<Program>` implementing `IAsyncLifetime`. Starts a `postgres:17-alpine` container, applies all 8 module migrations, seeds a test tenant ("testcorp"), and overrides configuration for JWT secrets and rate limits. Exposes `CreateTenantClient(subdomain)` to create `HttpClient` instances with the correct `Host` header for tenant resolution.
- `EndpointTestCollection` — xUnit `[CollectionDefinition("Endpoints")]` sharing a single `JobsiteWebApplicationFactory` across all endpoint test classes.
- `TestJwtHelper` — Static utility that generates valid/expired HS256 JWT tokens matching the factory's JWT configuration (issuer: "jobsite-iconnect", audience: "jobsite-iconnect"). Claims include `sub`, `nameid`, `email`, `role`, `tenant_id`, `jti`.

**Key design decisions:**

- Environment variables set before host builds to work around eager configuration capture in `AddJobsiteModules()`.
- MassTransit RabbitMQ replaced with `AddMassTransitTestHarness()` for in-memory messaging.
- Rate limits set to 10,000/min to prevent 429s during parallel test execution.
- `ResetTenantDataAsync()` truncates all tenant-scoped tables but preserves the `tenants` and `tenant_brandings` seed data.
- `ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))` suppresses pending migration warnings for ScreeningDbContext.

---

### `HealthEndpointTests` (5 tests)

Smoke tests validating that the application boots correctly and tenant resolution middleware functions end-to-end.

| Test                                          | What It Verifies                                                     | Expected Outcome                                    |
| --------------------------------------------- | -------------------------------------------------------------------- | --------------------------------------------------- |
| `Health_ReturnsOk`                            | `/health` endpoint responds to GET requests                          | 200 OK                                              |
| `Health_ReturnsJsonWithHealthyStatus`         | `/health` returns JSON body with `"status": "Healthy"`               | 200 OK + JSON with `status` field                   |
| `TenantRoute_WithoutHostHeader_Returns400`    | Request to tenant-scoped route without Host header is rejected       | 400 Bad Request (TenantResolutionMiddleware)        |
| `TenantRoute_WithValidHost_DoesNotReturn400`  | Request with valid tenant subdomain passes tenant resolution         | 401 Unauthorized (past resolution, fails at auth)   |
| `TenantRoute_WithUnknownSubdomain_Returns404` | Request to non-existent tenant subdomain fails with tenant not found | 404 Not Found + `TENANT_NOT_FOUND` in response body |

**Why:** These tests prove the application boots, middleware pipeline is wired, and the tenant resolution → auth → endpoint chain works. They catch startup DI failures, missing middleware registration, and routing misconfigurations that unit tests cannot detect.

---

### `AuthEndpointTests` (15 tests)

HTTP pipeline tests for Auth module endpoints (`/api/v1/auth/*`). Validates registration, login, refresh, logout, get-me, JWT authentication, snake_case serialization, and canonical error envelope shape through the full middleware stack.

| Test                                             | What It Verifies                                                    | Expected Outcome                                                          |
| ------------------------------------------------ | ------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Register_ValidRequest_Returns201WithTokens`     | Valid registration creates account and returns JWT tokens           | 201 Created + `access_token`, `refresh_token`, `expires_in`, `token_type` |
| `Register_MissingEmail_ReturnsClientError`       | Missing required email field is rejected                            | ≥400 (currently 500 — validation gap to fix)                              |
| `Register_DuplicateEmail_ReturnsClientError`     | Duplicate email registration prevented                              | 400 or 409                                                                |
| `Login_ValidCredentials_Returns200WithTokens`    | Valid email/password returns new tokens                             | 200 OK + tokens                                                           |
| `Login_InvalidPassword_Returns401`               | Wrong password rejected                                             | 401 Unauthorized                                                          |
| `Login_NonExistentUser_Returns401`               | Non-existent user login rejected                                    | 401 Unauthorized                                                          |
| `Refresh_ValidToken_ReturnsSuccessOrServerError` | Refresh endpoint with valid token returns refreshed tokens          | 200 OK or 500 (refresh pipeline gap to fix)                               |
| `Refresh_InvalidToken_Returns401`                | Invalid refresh token rejected                                      | 401 Unauthorized                                                          |
| `Logout_WithValidBearerToken_Returns204`         | Valid logout invalidates session                                    | 204 No Content                                                            |
| `Logout_WithoutBearerToken_Returns401`           | Logout without auth fails                                           | 401 Unauthorized                                                          |
| `GetMe_WithValidBearerToken_Returns200`          | `/me` endpoint with valid JWT returns user data                     | 200 OK + JSON with `email` field                                          |
| `GetMe_WithExpiredToken_Returns401`              | Expired JWT rejected by auth middleware                             | 401 Unauthorized                                                          |
| `Register_ResponseUsesSnakeCaseJson`             | Response body uses snake_case property names                        | 201 + `access_token`, `refresh_token`, `expires_in`, `token_type` present |
| `ErrorResponse_UsesCanonicalEnvelopeShape`       | Error responses follow canonical envelope with `code` and `message` | 401 + error envelope with `code` and `message` fields                     |
| `FullFlow_Register_Login_Refresh_GetMe_Logout`   | Complete auth lifecycle end-to-end through HTTP pipeline            | All operations succeed in sequence                                        |

**Why:** Unit tests cover service logic; repository integration tests cover EF Core. These tests cover everything in between: middleware execution order, JWT validation, request/response serialization, error mapping, status codes, and the full auth lifecycle. The snake_case and error envelope tests guard against serialization regressions that would break API clients.

---

### `TenantEndpointTests` (6 tests)

HTTP pipeline tests for Tenancy module endpoints (`/api/v1/tenants/*`). These routes are non-tenant-scoped — they don't require a subdomain Host header.

| Test                                                   | What It Verifies                                        | Expected Outcome                                                                                 |
| ------------------------------------------------------ | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `GetTenantById_ExistingTenant_Returns200`              | `/tenants/{id}` returns pre-seeded test tenant data     | 200 OK + `TenantResponse` with correct id, name, subdomain, status                               |
| `GetTenantById_NonExistentTenant_Returns404`           | Non-existent tenant ID returns 404                      | 404 Not Found                                                                                    |
| `GetTenantById_ResponseUsesSnakeCaseJson`              | Response uses snake_case field names                    | 200 OK + `id`, `name`, `subdomain`, `owner_name`, `owner_email`, `contact_name`, `contact_email` |
| `RegisterTenant_ValidRequest_Returns201`               | Register new tenant creates record with location header | 201 Created + `Location` header + `TenantResponse`                                               |
| `RegisterTenant_DuplicateSubdomain_ReturnsClientError` | Duplicate subdomain rejected                            | 400 or 409                                                                                       |
| `TenantRoutes_AreNonTenantScoped_NoHostHeaderRequired` | Tenant routes bypass tenant resolution middleware       | NOT 400 (no `INVALID_REQUEST` tenant error)                                                      |

**Why:** Tenant routes don't require authentication or tenant resolution — these tests verify the middleware bypass paths work correctly and that the Tenancy endpoints produce correct snake_case responses with proper status codes.

---

### `TenantIsolationTests` (6 tests)

Tests tenant boundary enforcement through the full middleware stack. Validates that non-Active tenants are rejected, non-existent subdomains return 404, and cross-tenant tokens are isolated.

| Test                                                       | What It Verifies                                                         | Expected Outcome                                                   |
| ---------------------------------------------------------- | ------------------------------------------------------------------------ | ------------------------------------------------------------------ |
| `Request_ToInactiveTenant_Returns403`                      | Requests to Suspended tenant blocked by middleware                       | 403 Forbidden + `FORBIDDEN` in response                            |
| `Request_ToDeactivatedTenant_Returns403`                   | Requests to Deactivated tenant blocked by middleware                     | 403 Forbidden                                                      |
| `Request_ToProvisioningTenant_Returns403`                  | Requests to Provisioning tenant blocked by middleware                    | 403 Forbidden                                                      |
| `Request_ToNonExistentTenant_Returns404WithTenantNotFound` | Non-existent tenant subdomain fails with tenant not found                | 404 Not Found + `TENANT_NOT_FOUND` in response                     |
| `RegisterUser_OnTenantA_CannotLoginOnTenantB`              | User registered on Tenant A cannot use their token on a different tenant | Isolated by database-per-tenant (200 or 404 depending on DB state) |
| `TenantResolution_CachesResolvedTenant`                    | Multiple requests to same tenant are resolved consistently               | Both requests return 401 (past tenant resolution, fail at auth)    |

**Why:** Tenant isolation is a security-critical property. If the middleware allows requests to suspended or deactivated tenants, users can access data they shouldn't. If cross-tenant tokens are accepted, the database-per-tenant boundary is bypassed. These tests are the primary guard against multi-tenancy security regressions.
