# Profiles Module Test Coverage

← [Test Coverage](README.md)

> Tests for applicant profiles, resume management, AI resume parsing, and cross-module event handling.

---

## `ProfileConstantsTests` (14 tests)

Tests `FileType.IsValid()` and `SkillLevel.IsValid()` constant validators. Values must match database CHECK constraints and are used for input validation throughout the module.

| Test                                                 | What It Verifies                                              | Expected Outcome |
| ---------------------------------------------------- | ------------------------------------------------------------- | ---------------- |
| `FileType_IsValid_ValidType_ReturnsTrue` [× 2]       | "PDF" and "DOCX" are valid file types                         | Returns `true`   |
| `FileType_IsValid_InvalidType_ReturnsFalse` [× 4]    | "DOC", "TXT", "pdf" (lowercase), and empty string are invalid | Returns `false`  |
| `SkillLevel_IsValid_ValidLevel_ReturnsTrue` [× 4]    | Beginner/Intermediate/Advanced/Expert are valid               | Returns `true`   |
| `SkillLevel_IsValid_InvalidLevel_ReturnsFalse` [× 4] | Lowercase, uppercase, and unknown values are invalid          | Returns `false`  |

**Why:** PascalCase validation is critical — the database CHECK constraint rejects lowercase variants. These tests catch casing mismatches before they reach PostgreSQL.

---

## `ProfileServiceTests` (12 tests)

Tests `ProfileService` — the application service for profile CRUD and profile completion evaluation. Uses NSubstitute to mock `IApplicantProfileRepository`, `IResumeRepository`, `ITenantSettingsReader`, `ILogger`, and keyed `IUnitOfWork`.

| Test                                                                   | What It Verifies                                       | Expected Outcome                                     |
| ---------------------------------------------------------------------- | ------------------------------------------------------ | ---------------------------------------------------- |
| `GetByUserIdAsync_ProfileExists_ReturnsProfileResponse`                | Existing profile maps entity to response DTO correctly | Response has matching userId, firstName, lastName    |
| `GetByUserIdAsync_ProfileNotFound_ThrowsAppError`                      | Missing profile throws `PROFILE_NOT_FOUND`             | Throws `AppError` with code `PROFILE_NOT_FOUND`      |
| `CreateAsync_ValidRequest_CreatesAndReturnsProfile`                    | Happy path: profile created with userId as PK          | `Add()` and `SaveChangesAsync()` called, DTO match   |
| `CreateAsync_ProfileAlreadyExists_ThrowsAppError`                      | Duplicate profile throws `PROFILE_ALREADY_EXISTS`      | Throws `AppError` with code `PROFILE_ALREADY_EXISTS` |
| `UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields`                   | JSON merge patch: only non-null fields are applied     | Updated field changes, others remain unchanged       |
| `UpdateAsync_ProfileNotFound_ThrowsAppError`                           | Missing profile on update throws `PROFILE_NOT_FOUND`   | Throws `AppError` with code `PROFILE_NOT_FOUND`      |
| `EvaluateProfileCompletion_AllRequirementsMet_SetsProfileCompletedAt`  | All tenant-configured requirements met                 | `ProfileCompletedAt` set to current timestamp        |
| `EvaluateProfileCompletion_MissingRequiredField_DoesNotComplete`       | Required field (e.g., Phone) is missing                | `ProfileCompletedAt` remains null                    |
| `EvaluateProfileCompletion_InsufficientSkills_DoesNotComplete`         | Skills count below `MinimumSkillsCount` threshold      | `ProfileCompletedAt` remains null                    |
| `EvaluateProfileCompletion_NoResume_WhenRequired_DoesNotComplete`      | `ResumeRequired=true` but no resume uploaded           | `ProfileCompletedAt` remains null                    |
| `EvaluateProfileCompletion_PreviouslyComplete_RevokesWhenFieldRemoved` | Profile was complete, then a required field is removed | `ProfileCompletedAt` cleared to null                 |
| `EvaluateProfileCompletion_NoSettings_DoesNotModifyCompletion`         | Tenant has no `ProfileSettings` configured             | `ProfileCompletedAt` not modified                    |

**Why:** Profile CRUD is the primary way applicants interact with their data. Merge patch semantics must not accidentally null out unrelated fields. The completion evaluation tests verify that tenant-configurable requirements (fields, skills count, social links, documents, resume) are correctly checked, and that completion status is revoked when requirements are no longer met.

---

## `ResumeServiceTests` (9 tests)

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

## `UserRegisteredProfileHandlerTests` (5 tests)

Tests the domain event handler that auto-creates an empty `ApplicantProfile` when a user registers with the Applicant role.

| Test                                                 | What It Verifies                                          | Expected Outcome                |
| ---------------------------------------------------- | --------------------------------------------------------- | ------------------------------- |
| `Handle_ApplicantRole_CreatesEmptyProfile`           | Applicant registration creates a profile with empty names | Profile added with userId as PK |
| `Handle_NonApplicantRole_SkipsProfileCreation` [× 3] | AgencyAdmin/HiringManager/Recruiter skip profile creation | `Add()` not called              |
| `Handle_ProfileAlreadyExists_SkipsCreation`          | Idempotent: existing profile prevents duplicate           | `Add()` not called              |

**Why:** This handler is a cross-module concern triggered by the Auth module's `UserRegisteredEvent`. It must only create profiles for Applicants and be idempotent to handle event redelivery.

---

## `CreateProfileRequestValidatorTests` (11 tests)

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

## `UpdateProfileRequestValidatorTests` (6 tests)

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

## `ResumeUploadedConsumerTests` (8 tests)

Tests `ResumeUploadedConsumer` — the MassTransit consumer that runs basic resume parsing asynchronously and publishes `ResumeParseRequested` to the message broker for AI parsing. Uses InMemory EF Core for `ProfilesDbContext` and NSubstitute for `IResumeParser`, `IEventPublisher`, `ITenantConnectionResolver`, and `ITenantDbContextFactory`.

| Test                                                          | What It Verifies                                                       | Expected Outcome                                |
| ------------------------------------------------------------- | ---------------------------------------------------------------------- | ----------------------------------------------- |
| `Consume_ValidEvent_RunsBasicParserAndPersists`               | Happy path: basic parser text + skills stored on resume                | ParsedText, ExtractedSkills set, IsParsed true  |
| `Consume_ValidEvent_PublishesResumeParseRequestedEvent`       | Publishes `ResumeParseRequested` event for async AI parsing            | Event published with correct data               |
| `Consume_ValidEvent_DoesNotStoreAiParsedContentSynchronously` | AI parsed content not set synchronously — arrives via broker           | AiParsedContent null after consumer completes   |
| `Consume_BasicParserFails_SetsParseErrorAndRethrows`          | Parser exception stored on resume, then rethrown for MassTransit retry | ParseError set, exception propagated            |
| `Consume_ResumeNotFound_LogsWarningAndReturns`                | Resume ID not in DB, parser never called                               | Parser not invoked                              |
| `Consume_AlreadyParsed_SkipsProcessing`                       | Resume with IsParsed=true is skipped                                   | Parser not invoked                              |
| `Consume_ValidEvent_ResolvesCorrectTenantConnection`          | Tenant connection resolved from event's TenantId                       | GetConnectionStringAsync called with correct ID |
| `Consume_SuccessfulParse_SetsParsedAtTimestamp`               | ParsedAt timestamp set on successful parse                             | ParsedAt is not null, >= test start time        |

**Why:** The consumer is the core of the async resume parsing pipeline. It runs basic parsing synchronously and publishes `ResumeParseRequested` to the broker for AI parsing. The AI result arrives asynchronously via `ResumeParsedConsumer`. The consumer must correctly resolve tenant databases for multi-tenant isolation.

---

## `ResumeParsedConsumerTests` (3 tests)

Tests `ResumeParsedConsumer` — the MassTransit consumer that processes `ResumeParsed` events from the AI Service, applying AI-extracted structured data to resume records.

| Test                                         | What It Verifies                                  | Expected Outcome                         |
| -------------------------------------------- | ------------------------------------------------- | ---------------------------------------- |
| `Consume_ValidEvent_UpdatesAiParsedContent`  | AI parsed content stored on resume record         | AiParsedContent populated                |
| `Consume_ResumeNotFound_DoesNotThrow`        | Missing resume handled gracefully                 | No exception, logs warning               |
| `Consume_ValidEvent_PreservesExistingFields` | AI update does not overwrite basic parsing fields | ParsedText and ExtractedSkills unchanged |

**Why:** This consumer is the receiving side of the async AI resume parsing pipeline. It must correctly store AI results without overwriting basic parser output, and handle edge cases gracefully.

---

## `BasicResumeParserTests` (8 tests)

Tests `BasicResumeParser` — PDF text extraction (PdfPig), DOCX extraction (OpenXml), and keyword skill matching. Uses real temp files created with PdfPig `PdfDocumentBuilder` and OpenXml `WordprocessingDocument`.

| Test                                                         | What It Verifies                                       | Expected Outcome                        |
| ------------------------------------------------------------ | ------------------------------------------------------ | --------------------------------------- |
| `ParseAsync_ValidPdf_ExtractsText`                           | PdfPig extracts text from a generated PDF              | ParsedText contains expected text       |
| `ParseAsync_ValidDocx_ExtractsText`                          | OpenXml extracts text from a generated DOCX            | ParsedText contains expected text       |
| `ParseAsync_TextWithKnownSkills_ExtractsMatchingSkills`      | Text containing "C#, Python, SQL" matches known skills | ExtractedSkills JSON contains all three |
| `ParseAsync_TextWithNoSkills_ReturnsNullSkills`              | Text with no skill keywords returns null               | ExtractedSkills is null                 |
| `ParseAsync_SkillMatchingIsCaseInsensitive`                  | "python" matches "Python" in the skills list           | ExtractedSkills contains "Python"       |
| `ParseAsync_UnsupportedFileType_ThrowsNotSupportedException` | Non-PDF/DOCX file type rejected                        | Throws `NotSupportedException`          |
| `ParseAsync_FileNotFound_ThrowsFileNotFoundException`        | Missing file throws before type check                  | Throws `FileNotFoundException`          |
| `ParseAsync_DocxFile_ExtractsSkillsFromDocx`                 | DOCX file with skills extracts correct matches         | ExtractedSkills contains matches        |

**Why:** Resume parsing is the data backbone for screening. PdfPig and OpenXml are real libraries with nuanced behavior — in-memory test files validate the extraction pipeline end-to-end without external dependencies.

---

## `LocalFileStorageTests` (6 tests)

Tests `LocalFileStorage` — the `IFileStorage` implementation for local filesystem uploads. Uses temp directories with cleanup in `Dispose()`.

| Test                                                | What It Verifies                                 | Expected Outcome                 |
| --------------------------------------------------- | ------------------------------------------------ | -------------------------------- |
| `UploadAsync_ValidFile_WritesToDisk`                | File bytes written to expected path              | File exists with correct content |
| `UploadAsync_SanitizesFilename_RemovesInvalidChars` | Invalid filename chars replaced with underscores | No invalid chars in output       |
| `UploadAsync_LongFilename_TruncatesTo200Chars`      | 300-char filename truncated to 200               | Sanitized part ≤ 200 chars       |
| `UploadAsync_CreatesDirectoryIfNotExists`           | "resumes" subdirectory auto-created              | Directory exists                 |
| `DeleteAsync_ExistingFile_RemovesFromDisk`          | Uploaded file successfully deleted               | File no longer exists            |
| `DeleteAsync_NonExistentFile_DoesNotThrow`          | Missing file silently ignored                    | No exception thrown              |

**Why:** File storage is a security-sensitive boundary. Filename sanitization prevents path traversal, and the truncation test guards against filesystem limits. Delete idempotency prevents errors during retry scenarios.

---

## `ResumeParseRecoveryServiceTests` (5 tests)

Tests `ResumeParseRecoveryService` — a `BackgroundService` that re-publishes `ResumeUploadedEvent` for resumes interrupted during previous runs. Uses InMemory EF Core and NSubstitute for `IServiceScopeFactory`, `ITenantConnectionResolver`, and `IEventPublisher`.

| Test                                                  | What It Verifies                             | Expected Outcome              |
| ----------------------------------------------------- | -------------------------------------------- | ----------------------------- |
| `ExecuteAsync_UnparsedResumesExist_RepublishesEvents` | Finds 2 unparsed resumes, publishes 2 events | 2 events published            |
| `ExecuteAsync_NoUnparsedResumes_PublishesNothing`     | Already-parsed resumes produce no events     | Zero events published         |
| `ExecuteAsync_MultipleTenants_ProcessesAllTenants`    | Iterates across 2 tenant connections         | Events published for both     |
| `ExecuteAsync_TenantFails_ContinuesWithOtherTenants`  | Exception in tenant A doesn't block tenant B | Tenant B still processed      |
| `ExecuteAsync_OnlyReprocessesNotParsedAndNoError`     | Resumes with ParseError set are skipped      | Only clean unparsed re-queued |

**Why:** The recovery service ensures resume parsing resilience — if the app crashes mid-parse, unparsed resumes are automatically re-queued on restart. Multi-tenant fault isolation is critical.

---

## `ProfilesDbContextTests` — Integration (10 tests)

Tests ProfilesDbContext schema creation, entity CRUD, CHECK constraints, JSONB column mapping, cascade deletes, and indexes against a real PostgreSQL container via Testcontainers.

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

**Why:** EF Core JSONB mapping, CHECK constraints, cascade deletes, and index configurations can only be validated against real PostgreSQL. Unit tests with mocks cannot catch column type mismatches or missing configurations.

---

## `ProfilesRepositoryTests` — Integration (12 tests)

Tests `ApplicantProfileRepository` and `ResumeRepository` against a real PostgreSQL container. Validates CRUD operations, tracking behavior, ordering, and bulk updates.

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

**Why:** Repository integration tests catch EF Core query translation issues, AsNoTracking vs tracked entity behavior, ExecuteUpdate bulk operations, and ordering logic that only surface against real PostgreSQL.

---

## Coverage Gaps

### Integration Test Gaps

| Area                         | Gap                                                                                                                         | Priority |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------------- | -------- |
| **Endpoint Tests**           | No `WebApplicationFactory` HTTP pipeline tests for profile CRUD (`GET/POST/PATCH /api/v1/profiles/me`) or resume endpoints. | Medium   |
| **MassTransit Consumer E2E** | No end-to-end test with Testcontainers RabbitMQ for the resume upload → parse pipeline.                                     | Medium   |
| **AzureBlobFileStorage**     | No integration tests for Azure Blob Storage `IFileStorage` implementation. `LocalFileStorage` has 6 unit tests.             | Low      |

### Blocked by AI Service (Phase 6)

| Area                               | Gap                                                                                                                                       |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| **Full Resume Parse Pipeline E2E** | End-to-end resume upload → basic parse → broker publish → AI parse → broker consume → persist requires operational AI Service + RabbitMQ. |
