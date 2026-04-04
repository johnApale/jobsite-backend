# Profiles Module Test Coverage

← [Test Coverage](README.md)

> Tests for applicant profiles, resume management, AI resume parsing, and cross-module event handling.

---

## `ProfileConstantsTests` (8 tests)

Tests `FileType.IsValid()` and `SkillLevel.IsValid()` constant validators. Values must match database CHECK constraints and are used for input validation throughout the module.

| Test                                                 | What It Verifies                                              | Expected Outcome |
| ---------------------------------------------------- | ------------------------------------------------------------- | ---------------- |
| `FileType_IsValid_ValidType_ReturnsTrue` [× 2]       | "PDF" and "DOCX" are valid file types                         | Returns `true`   |
| `FileType_IsValid_InvalidType_ReturnsFalse` [× 4]    | "DOC", "TXT", "pdf" (lowercase), and empty string are invalid | Returns `false`  |
| `SkillLevel_IsValid_ValidLevel_ReturnsTrue` [× 4]    | Beginner/Intermediate/Advanced/Expert are valid               | Returns `true`   |
| `SkillLevel_IsValid_InvalidLevel_ReturnsFalse` [× 4] | Lowercase, uppercase, and unknown values are invalid          | Returns `false`  |

**Why:** PascalCase validation is critical — the database CHECK constraint rejects lowercase variants. These tests catch casing mismatches before they reach PostgreSQL.

---

## `ProfileServiceTests` (5 tests)

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

## `ResumeServiceTests` (8 tests)

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

Tests the MediatR handler that auto-creates an empty `ApplicantProfile` when a user registers with the Applicant role.

| Test                                                 | What It Verifies                                          | Expected Outcome                |
| ---------------------------------------------------- | --------------------------------------------------------- | ------------------------------- |
| `Handle_ApplicantRole_CreatesEmptyProfile`           | Applicant registration creates a profile with empty names | Profile added with userId as PK |
| `Handle_NonApplicantRole_SkipsProfileCreation` [× 3] | AgencyAdmin/HiringManager/Recruiter skip profile creation | `Add()` not called              |
| `Handle_ProfileAlreadyExists_SkipsCreation`          | Idempotent: existing profile prevents duplicate           | `Add()` not called              |

**Why:** This handler is a cross-module concern triggered by the Auth module's `UserRegisteredEvent`. It must only create profiles for Applicants and be idempotent to handle event redelivery.

---

## `CreateProfileRequestValidatorTests` (9 tests)

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

## `AiResumeParserClientTests` (5 tests)

Tests `AiResumeParserClient` — the HTTP client for the AI Service's resume parsing endpoint. Uses a fake `HttpMessageHandler` to simulate various failure modes.

| Test                                          | What It Verifies                                                  | Expected Outcome |
| --------------------------------------------- | ----------------------------------------------------------------- | ---------------- |
| `ParseAsync_SuccessResponse_ReturnsResult`    | 200 OK with valid JSON returns deserialized `AiResumeParseResult` | Non-null result  |
| `ParseAsync_ErrorResponse_ReturnsNull`        | 500 response returns null (graceful fallback)                     | Returns `null`   |
| `ParseAsync_HttpRequestException_ReturnsNull` | Connection refused returns null (AI Service down)                 | Returns `null`   |
| `ParseAsync_TaskCanceled_ReturnsNull`         | Request timeout returns null (resilience policy triggered)        | Returns `null`   |
| `ParseAsync_InvalidJson_ReturnsNull`          | Malformed JSON response returns null                              | Returns `null`   |

**Why:** The AI parser client must never throw exceptions to the consumer. All failure modes must be handled gracefully with null return, allowing the basic parser output to be used as the sole result. This ensures resume processing is never blocked by AI Service availability.
