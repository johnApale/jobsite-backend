# Recruitment Module Test Coverage

← [Test Coverage](README.md)

> Tests for job postings, applications, client companies, evaluation criteria, screening questions, AI suggestion clients, and input validation.

---

## `JobPostingTests` (4 tests)

Tests the `JobPosting` aggregate root's domain behavior — status lifecycle transitions and default state.

| Test                                               | What It Verifies                                                | Expected Outcome                             |
| -------------------------------------------------- | --------------------------------------------------------------- | -------------------------------------------- |
| `Publish_DraftJobPosting_SetsStatusAndTimestamp`   | Publishing sets status to `Published` and records `PublishedAt` | Status is Published, PublishedAt is not null |
| `Close_PublishedJobPosting_SetsStatusAndTimestamp` | Closing sets status to `Closed` and records `ClosedAt`          | Status is Closed, ClosedAt is not null       |
| `NewJobPosting_HasDefaultDraftStatus`              | A new job posting starts in Draft status                        | Status is `Draft`                            |
| `NewJobPosting_HasEmptyCollections`                | Criteria and Questions collections are initialized empty        | Both collections are empty                   |

**Why:** Job postings follow a strict Draft → Published → Closed lifecycle. If status transitions set incorrect values or timestamps aren't recorded, the entire recruitment pipeline breaks — applications can only be submitted to Published postings.

---

## `ApplicationTests` (5 tests)

Tests the `Application` aggregate root's domain behavior — submission events and withdrawal.

| Test                                                      | What It Verifies                                                                  | Expected Outcome                              |
| --------------------------------------------------------- | --------------------------------------------------------------------------------- | --------------------------------------------- |
| `Submit_SetsStatusAndTimestampAndRaisesDomainEvent`       | Submitting sets status, records timestamp, and raises `ApplicationSubmittedEvent` | Status is Submitted, DomainEvents has 1 event |
| `Submit_WithQuestionAnswers_IncludesAnswersInDomainEvent` | Submission with question answers includes them in the domain event                | QuestionAnswers in event is populated         |
| `Submit_DomainEventContainsCorrectIds`                    | The domain event carries the correct application and job posting IDs              | Event IDs match entity IDs                    |
| `Withdraw_SetsStatusToWithdrawnAndTimestamp`              | Withdrawing sets status to `Withdrawn` and records `WithdrawnAt`                  | Status is Withdrawn, WithdrawnAt is set       |
| `NewApplication_DefaultStatusIsSubmitted`                 | A new application starts in Submitted status                                      | Status is `Submitted`                         |

**Why:** `ApplicationSubmittedEvent` is the trigger for the entire downstream pipeline (Screening → Assessment → Shortlisting). If this event isn't raised or carries wrong IDs, screening never starts.

---

## `CreateJobPostingRequestValidatorTests` (10 tests)

Tests FluentValidation rules for the `CreateJobPostingRequest` DTO.

| Test                                     | What It Verifies                                  | Expected Outcome |
| ---------------------------------------- | ------------------------------------------------- | ---------------- |
| `Validate_ValidRequest_Passes`           | A well-formed request passes all validation rules | No errors        |
| `Validate_EmptyTitle_Fails`              | Empty title is rejected                           | Validation error |
| `Validate_TitleTooLong_Fails`            | Title exceeding 200 chars is rejected             | Validation error |
| `Validate_InvalidLocationType_Fails`     | Invalid location type value is rejected           | Validation error |
| `Validate_OnSiteWithoutCity_Fails`       | OnSite location without city is rejected          | Validation error |
| `Validate_RemoteWithoutCity_Passes`      | Remote location without city is acceptable        | No errors        |
| `Validate_InvalidEmploymentType_Fails`   | Invalid employment type value is rejected         | Validation error |
| `Validate_SalaryMinGreaterThanMax_Fails` | Salary min > max is rejected                      | Validation error |
| `Validate_SalaryWithoutCurrency_Fails`   | Providing salary without currency is rejected     | Validation error |
| `Validate_NegativeSalary_Fails`          | Negative salary value is rejected                 | Validation error |

**Why:** These rules enforce database CHECK constraints and business invariants at the API boundary. Invalid data that passes validation would cause DB constraint violations or corrupt data.

---

## `SubmitApplicationRequestValidatorTests` (4 tests)

Tests FluentValidation rules for the `SubmitApplicationRequest` DTO.

| Test                                       | What It Verifies                                             | Expected Outcome |
| ------------------------------------------ | ------------------------------------------------------------ | ---------------- |
| `Validate_ValidRequest_Passes`             | A well-formed submission passes                              | No errors        |
| `Validate_EmptyResumeId_Fails`             | Empty resume ID is rejected                                  | Validation error |
| `Validate_EmptyQuestionId_InAnswers_Fails` | Question answer with empty question ID is rejected           | Validation error |
| `Validate_NullQuestionAnswers_Passes`      | Null question answers (no screening questions) is acceptable | No errors        |

**Why:** Resume ID is required for every application. Question answers with empty IDs would create orphaned responses that can't be scored.

---

## `RecruitmentServiceTests` (12 tests)

Tests the `RecruitmentService` which manages job posting CRUD and lifecycle transitions.

| Test                                                             | What It Verifies                               | Expected Outcome             |
| ---------------------------------------------------------------- | ---------------------------------------------- | ---------------------------- |
| `CreateAsync_ValidRequest_ReturnsResponseWithDraftStatus`        | Creates a job posting with Draft status        | Posting saved to repository  |
| `CreateAsync_WithClientCompanyId_ValidatesCompanyExists`         | Client company ID is validated before creation | Company ID matches           |
| `CreateAsync_InvalidClientCompanyId_ThrowsClientCompanyNotFound` | Non-existent client company throws error       | AppError thrown              |
| `GetByIdAsync_ExistingId_ReturnsResponse`                        | Returns mapped response for existing posting   | Response matches entity      |
| `GetByIdAsync_NonExistentId_ThrowsJobPostingNotFound`            | Non-existent ID throws not found error         | AppError thrown              |
| `UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields`             | JSON merge patch applies only non-null fields  | Updated fields match request |
| `UpdateAsync_NonExistentId_ThrowsJobPostingNotFound`             | Update on non-existent posting throws error    | AppError thrown              |
| `PublishAsync_DraftJobPosting_TransitionsToPublished`            | Draft posting transitions to Published         | Status is Published          |
| `PublishAsync_NonDraftJobPosting_ThrowsUnprocessableEntity`      | Publishing non-Draft posting throws error      | AppError thrown              |
| `PublishAsync_NonExistentId_ThrowsJobPostingNotFound`            | Publishing non-existent posting throws error   | AppError thrown              |
| `CloseAsync_PublishedJobPosting_TransitionsToClosed`             | Published posting transitions to Closed        | Status is Closed             |
| `CloseAsync_NonPublishedJobPosting_ThrowsUnprocessableEntity`    | Closing non-Published posting throws error     | AppError thrown              |

**Why:** The service enforces the job posting lifecycle (Draft → Published → Closed) which gates the entire application flow. Invalid transitions would allow applications to non-published jobs or modifications to published postings.

---

## `ApplicationServiceTests` (11 tests)

Tests the `ApplicationService` which handles application submission, retrieval, and withdrawal.

| Test                                                          | What It Verifies                                                | Expected Outcome                |
| ------------------------------------------------------------- | --------------------------------------------------------------- | ------------------------------- |
| `SubmitAsync_ValidRequest_ReturnsResponseWithSubmittedStatus` | Successful submission saves application and raises domain event | Application saved, event raised |
| `SubmitAsync_NonPublishedJob_ThrowsUnprocessableEntity`       | Submitting to non-Published job throws error                    | AppError thrown                 |
| `SubmitAsync_NonExistentJob_ThrowsJobPostingNotFound`         | Submitting to non-existent job throws error                     | AppError thrown                 |
| `SubmitAsync_DuplicateApplication_ThrowsDuplicateApplication` | Same applicant applying twice throws error                      | AppError thrown                 |
| `SubmitAsync_ResumeNotOwned_ThrowsResumeNotFound`             | Using another user's resume throws error                        | AppError thrown                 |
| `GetByIdAsync_ExistingId_ReturnsResponse`                     | Returns mapped response for existing application                | Response matches entity         |
| `GetByIdAsync_NonExistentId_ThrowsApplicationNotFound`        | Non-existent application throws error                           | AppError thrown                 |
| `WithdrawAsync_OwnApplication_TransitionsToWithdrawn`         | Applicant can withdraw their own application                    | Status is Withdrawn             |
| `WithdrawAsync_OtherUsersApplication_ThrowsForbidden`         | Withdrawing another user's application throws error             | AppError thrown                 |
| `WithdrawAsync_AlreadyWithdrawn_ThrowsAlreadyWithdrawn`       | Withdrawing an already withdrawn application throws error       | AppError thrown                 |
| `WithdrawAsync_NonExistentId_ThrowsApplicationNotFound`       | Withdrawing non-existent application throws error               | AppError thrown                 |

**Why:** Application submission is the entry point to the recruitment pipeline. The one-per-person-per-job constraint, resume ownership validation, and posting status checks prevent data corruption and unauthorized access.

---

## `ClientCompanyServiceTests` (5 tests)

Tests the `ClientCompanyService` CRUD operations.

| Test                                           | What It Verifies                               | Expected Outcome        |
| ---------------------------------------------- | ---------------------------------------------- | ----------------------- |
| `CreateAsync_ValidRequest_CreatesCompany`      | Creates client company with correct properties | Company saved to repo   |
| `GetByIdAsync_ExistingCompany_ReturnsResponse` | Returns mapped response for existing company   | Response matches entity |
| `GetByIdAsync_NonExistent_ThrowsNotFound`      | Non-existent company throws error              | AppError thrown         |
| `UpdateAsync_ValidRequest_AppliesMergePatch`   | JSON merge patch applies only non-null fields  | Updated fields match    |
| `UpdateAsync_NonExistent_ThrowsNotFound`       | Update on non-existent company throws error    | AppError thrown         |

**Why:** Client companies support the agency model. Invalid CRUD operations would corrupt the relationship between job postings and their client companies.

---

## `CriteriaServiceTests` (9 tests)

Tests the `CriteriaService` for managing evaluation criteria and AI-assisted suggestions.

| Test                                                     | What It Verifies                                 | Expected Outcome         |
| -------------------------------------------------------- | ------------------------------------------------ | ------------------------ |
| `AddAsync_ValidRequest_ReturnsResponse`                  | Creates criterion linked to job posting          | Criterion saved          |
| `AddAsync_NonExistentJob_ThrowsJobPostingNotFound`       | Adding criteria to non-existent job throws error | AppError thrown          |
| `UpdateAsync_ValidRequest_UpdatesFields`                 | JSON merge patch updates only provided fields    | Updated fields match     |
| `UpdateAsync_WrongJobPostingId_ThrowsCriteriaNotFound`   | Updating criterion from another job throws error | AppError thrown          |
| `DeleteAsync_ExistingCriteria_RemovesAndSaves`           | Deletes criterion successfully                   | Repository Remove called |
| `DeleteAsync_NonExistentCriteria_ThrowsCriteriaNotFound` | Deleting non-existent criterion throws error     | AppError thrown          |
| `SuggestAsync_AiAvailable_ReturnsSuggestions`            | AI service returns suggestions when available    | Suggestions returned     |
| `SuggestAsync_AiUnavailable_ReturnsNull`                 | Returns null when AI service is unavailable      | Returns null             |
| `SuggestAsync_NonExistentJob_ThrowsJobPostingNotFound`   | Suggesting for non-existent job throws error     | AppError thrown          |

**Why:** Evaluation criteria drive the Screening module's scoring engine. The weights, categories, and evaluation methods must be valid and correctly linked to job postings for accurate candidate scoring.

---

## `ScreeningQuestionServiceTests` (9 tests)

Tests the `ScreeningQuestionService` for managing screening questions and feature-gated AI suggestions.

| Test                                                              | What It Verifies                                              | Expected Outcome         |
| ----------------------------------------------------------------- | ------------------------------------------------------------- | ------------------------ |
| `AddAsync_ValidRequest_ReturnsResponse`                           | Creates question linked to job posting                        | Question saved           |
| `AddAsync_NonExistentJob_ThrowsJobPostingNotFound`                | Adding question to non-existent job throws error              | AppError thrown          |
| `UpdateAsync_ValidRequest_UpdatesFields`                          | JSON merge patch updates only provided fields                 | Updated fields match     |
| `UpdateAsync_WrongJobPostingId_ThrowsScreeningQuestionNotFound`   | Updating question from another job throws error               | AppError thrown          |
| `DeleteAsync_ExistingQuestion_RemovesAndSaves`                    | Deletes question successfully                                 | Repository Remove called |
| `DeleteAsync_NonExistentQuestion_ThrowsScreeningQuestionNotFound` | Deleting non-existent question throws error                   | AppError thrown          |
| `SuggestAsync_FeatureDisabled_ReturnsNull`                        | Returns null when tenant setting disables AI questions        | Returns null             |
| `SuggestAsync_FeatureEnabled_ReturnsSuggestions`                  | AI service returns suggestions when feature is enabled        | Suggestions returned     |
| `SuggestAsync_NullSettings_ReturnsNull`                           | Returns null when company settings are null (not provisioned) | Returns null             |

**Why:** Screening questions are the foundation of the assessment flow. The feature gate for AI suggestions ensures tenant-level control over AI features. Questions with wrong job posting links would cause assessment to fail.

---

## `RecruitmentConstantsTests` (20 tests)

Tests `IsValid()` methods for all 10 Recruitment module constant classes. Values must match PostgreSQL CHECK constraints exactly.

| Constant Class        | Tests | What It Verifies                                                   |
| --------------------- | ----- | ------------------------------------------------------------------ |
| `JobPostingStatus`    | 2     | Draft/Published/Closed valid; lowercase and unknown values invalid |
| `ApplicationStatus`   | 2     | All 9 statuses valid; lowercase and unknown values invalid         |
| `LocationType`        | 2     | OnSite/Remote/Hybrid valid; lowercase and unknown values invalid   |
| `EmploymentType`      | 2     | All 5 types valid; lowercase and unknown values invalid            |
| `CriteriaCategory`    | 2     | All 6 categories valid; lowercase and unknown values invalid       |
| `EvaluationMethod`    | 2     | All 3 methods valid; lowercase and unknown values invalid          |
| `QuestionType`        | 2     | All 3 types valid; lowercase and unknown values invalid            |
| `QuestionTiming`      | 2     | AtApplication/AfterScreening valid; lowercase and unknown invalid  |
| `RejectedAtStage`     | 2     | All 5 stages valid; lowercase and unknown values invalid           |
| `ClientCompanyStatus` | 2     | Active/Inactive valid; lowercase and unknown values invalid        |

**Why:** PascalCase enum values must exactly match PostgreSQL CHECK constraints. Lowercase or unknown values that pass `IsValid()` would violate DB constraints. These tests guard every status/type field in the module.

---

## `AiCriteriaSuggesterClientTests` (5 tests)

Tests the `AiCriteriaSuggesterClient` HTTP client that calls the AI Service for criteria suggestions.

| Test                                              | What It Verifies                                        | Expected Outcome |
| ------------------------------------------------- | ------------------------------------------------------- | ---------------- |
| `SuggestAsync_SuccessResponse_ReturnsSuggestions` | Successful AI response returns deserialized suggestions | Suggestions list |
| `SuggestAsync_ErrorResponse_ReturnsNull`          | Non-success HTTP status returns null                    | Returns null     |
| `SuggestAsync_HttpRequestException_ReturnsNull`   | Network errors return null                              | Returns null     |
| `SuggestAsync_TaskCanceled_ReturnsNull`           | Timeout returns null                                    | Returns null     |
| `SuggestAsync_InvalidJson_ReturnsNull`            | Malformed JSON response returns null                    | Returns null     |

**Why:** The AI criteria client must never throw exceptions to callers. All failure modes return null, allowing the application to gracefully handle AI Service unavailability by returning 204 No Content to the user.

---

## `AiQuestionSuggesterClientTests` (5 tests)

Tests the `AiQuestionSuggesterClient` HTTP client that calls the AI Service for screening question suggestions.

| Test                                              | What It Verifies                                        | Expected Outcome |
| ------------------------------------------------- | ------------------------------------------------------- | ---------------- |
| `SuggestAsync_SuccessResponse_ReturnsSuggestions` | Successful AI response returns deserialized suggestions | Suggestions list |
| `SuggestAsync_ErrorResponse_ReturnsNull`          | Non-success HTTP status returns null                    | Returns null     |
| `SuggestAsync_HttpRequestException_ReturnsNull`   | Network errors return null                              | Returns null     |
| `SuggestAsync_TaskCanceled_ReturnsNull`           | Timeout returns null                                    | Returns null     |
| `SuggestAsync_InvalidJson_ReturnsNull`            | Malformed JSON response returns null                    | Returns null     |

**Why:** Same resilience pattern as the criteria client. AI question suggestions are feature-gated and optional — failures must never block the core recruitment workflow.

---

## `UpdateJobPostingRequestValidatorTests` (11 tests)

Tests FluentValidation rules for the `UpdateJobPostingRequest` merge-patch DTO. All fields are nullable — validation fires only when a field is provided.

| Test                                     | What It Verifies                                   | Expected Outcome |
| ---------------------------------------- | -------------------------------------------------- | ---------------- |
| `Validate_AllNullFields_Passes`          | Empty merge-patch (no-op update) passes validation | No errors        |
| `Validate_ValidPartialUpdate_Passes`     | Partial update with valid values passes            | No errors        |
| `Validate_TitleEmpty_WhenProvided_Fails` | Non-null but empty title is rejected               | Validation error |
| `Validate_TitleTooLong_Fails`            | Title exceeding 200 chars is rejected              | Validation error |
| `Validate_InvalidLocationType_Fails`     | Invalid location type value is rejected            | Validation error |
| `Validate_InvalidEmploymentType_Fails`   | Invalid employment type value is rejected          | Validation error |
| `Validate_NegativeSalaryMin_Fails`       | Negative salary minimum is rejected                | Validation error |
| `Validate_SalaryMaxLessThanMin_Fails`    | Salary max less than min is rejected               | Validation error |
| `Validate_SalaryCurrencyTooLong_Fails`   | Currency code exceeding 3 chars is rejected        | Validation error |
| `Validate_CityTooLong_Fails`             | City exceeding 100 chars is rejected               | Validation error |
| `Validate_DepartmentTooLong_Fails`       | Department exceeding 100 chars is rejected         | Validation error |

**Why:** Merge-patch DTOs have nullable fields — validation must only fire when a field is explicitly provided. These tests ensure the `.When(x => x.Field is not null)` guards work correctly, preventing false rejections on legitimate no-op updates.

---

## `CreateClientCompanyRequestValidatorTests` (10 tests)

Tests FluentValidation rules for the `CreateClientCompanyRequest` DTO.

| Test                                  | What It Verifies                         | Expected Outcome |
| ------------------------------------- | ---------------------------------------- | ---------------- |
| `Validate_ValidRequest_Passes`        | Well-formed request passes all rules     | No errors        |
| `Validate_MinimalValidRequest_Passes` | Only required field (Name) is sufficient | No errors        |
| `Validate_EmptyName_Fails`            | Empty name is rejected                   | Validation error |
| `Validate_NameTooLong_Fails`          | Name exceeding 200 chars is rejected     | Validation error |
| `Validate_DisplayNameTooLong_Fails`   | Display name exceeding 200 chars         | Validation error |
| `Validate_InvalidIndustry_Fails`      | Invalid industry value is rejected       | Validation error |
| `Validate_WebsiteTooLong_Fails`       | Website exceeding 2048 chars             | Validation error |
| `Validate_ContactNameTooLong_Fails`   | Contact name exceeding 200 chars         | Validation error |
| `Validate_InvalidContactEmail_Fails`  | Invalid email format is rejected         | Validation error |
| `Validate_ContactPhoneTooLong_Fails`  | Contact phone exceeding 20 chars         | Validation error |

**Why:** Client company data populates job listing displays and internal recruiter tools. Invalid data creates visual bugs, broken links, or email delivery failures.

---

## `UpdateClientCompanyRequestValidatorTests` (9 tests)

Tests FluentValidation rules for the `UpdateClientCompanyRequest` merge-patch DTO.

| Test                                 | What It Verifies                                   | Expected Outcome |
| ------------------------------------ | -------------------------------------------------- | ---------------- |
| `Validate_AllNullFields_Passes`      | Empty merge-patch (no-op update) passes validation | No errors        |
| `Validate_ValidPartialUpdate_Passes` | Partial update with valid values passes            | No errors        |
| `Validate_EmptyName_Fails`           | Non-null but empty name is rejected                | Validation error |
| `Validate_NameTooLong_Fails`         | Name exceeding 200 chars is rejected               | Validation error |
| `Validate_InvalidIndustry_Fails`     | Invalid industry value is rejected                 | Validation error |
| `Validate_InvalidStatus_Fails`       | Invalid company status is rejected                 | Validation error |
| `Validate_InvalidContactEmail_Fails` | Invalid email format is rejected                   | Validation error |
| `Validate_ContactPhoneTooLong_Fails` | Contact phone exceeding 20 chars is rejected       | Validation error |
| `Validate_WebsiteTooLong_Fails`      | Website exceeding 2048 chars is rejected           | Validation error |

**Why:** Same merge-patch pattern as job posting updates. The Status field is unique to the update DTO — it allows transitioning companies between Active/Inactive, which gates new job creation for that client.

---

## `CreateCriteriaRequestValidatorTests` (10 tests)

Tests FluentValidation rules for the `CreateCriteriaRequest` DTO.

| Test                                     | What It Verifies                      | Expected Outcome |
| ---------------------------------------- | ------------------------------------- | ---------------- |
| `Validate_ValidRequest_Passes`           | Well-formed request passes all rules  | No errors        |
| `Validate_EmptyName_Fails`               | Empty criterion name is rejected      | Validation error |
| `Validate_NameTooLong_Fails`             | Name exceeding 200 chars is rejected  | Validation error |
| `Validate_InvalidCategory_Fails`         | Invalid category value is rejected    | Validation error |
| `Validate_EmptyCategory_Fails`           | Empty category is rejected            | Validation error |
| `Validate_InvalidEvaluationMethod_Fails` | Invalid evaluation method is rejected | Validation error |
| `Validate_EmptyEvaluationMethod_Fails`   | Empty evaluation method is rejected   | Validation error |
| `Validate_WeightBelowZero_Fails`         | Negative weight is rejected           | Validation error |
| `Validate_WeightAbove100_Fails`          | Weight exceeding 100 is rejected      | Validation error |
| `Validate_NegativeDisplayOrder_Fails`    | Negative display order is rejected    | Validation error |

**Why:** Criteria drive the Screening module's scoring engine. Invalid categories or evaluation methods would cause the scoring engine to return zero scores for all candidates. Weight values outside 0–100 would break the weighted average calculation.

---

## `UpdateCriteriaRequestValidatorTests` (10 tests)

Tests FluentValidation rules for the `UpdateCriteriaRequest` merge-patch DTO.

| Test                                     | What It Verifies                                   | Expected Outcome |
| ---------------------------------------- | -------------------------------------------------- | ---------------- |
| `Validate_AllNullFields_Passes`          | Empty merge-patch (no-op update) passes validation | No errors        |
| `Validate_ValidPartialUpdate_Passes`     | Partial update with valid values passes            | No errors        |
| `Validate_AllValidFields_Passes`         | Full update with all valid values passes           | No errors        |
| `Validate_EmptyName_Fails`               | Non-null but empty name is rejected                | Validation error |
| `Validate_NameTooLong_Fails`             | Name exceeding 200 chars is rejected               | Validation error |
| `Validate_InvalidCategory_Fails`         | Invalid category value is rejected                 | Validation error |
| `Validate_InvalidEvaluationMethod_Fails` | Invalid evaluation method is rejected              | Validation error |
| `Validate_WeightBelowZero_Fails`         | Negative weight is rejected                        | Validation error |
| `Validate_WeightAbove100_Fails`          | Weight exceeding 100 is rejected                   | Validation error |
| `Validate_NegativeDisplayOrder_Fails`    | Negative display order is rejected                 | Validation error |

**Why:** Same merge-patch guarding pattern. Prevents invalid partial updates from corrupting existing criteria data that the Screening module depends on for scoring.

---

## `CreateQuestionRequestValidatorTests` (11 tests)

Tests FluentValidation rules for the `CreateQuestionRequest` DTO.

| Test                                          | What It Verifies                                    | Expected Outcome |
| --------------------------------------------- | --------------------------------------------------- | ---------------- |
| `Validate_ValidRequest_Passes`                | Well-formed request passes all rules                | No errors        |
| `Validate_EmptyQuestionText_Fails`            | Empty question text is rejected                     | Validation error |
| `Validate_InvalidQuestionType_Fails`          | Invalid question type value is rejected             | Validation error |
| `Validate_InvalidTiming_Fails`                | Invalid timing value is rejected                    | Validation error |
| `Validate_WeightBelowZero_Fails`              | Negative weight is rejected                         | Validation error |
| `Validate_WeightAbove100_Fails`               | Weight exceeding 100 is rejected                    | Validation error |
| `Validate_MultipleChoiceWithoutOptions_Fails` | MultipleChoice question without options is rejected | Validation error |
| `Validate_MultipleChoiceWithOptions_Passes`   | MultipleChoice question with options passes         | No errors        |
| `Validate_YesNoWithoutOptions_Passes`         | YesNo question without options is acceptable        | No errors        |
| `Validate_NegativeDisplayOrder_Fails`         | Negative display order is rejected                  | Validation error |
| `Validate_EmptyQuestionType_Fails`            | Empty question type is rejected                     | Validation error |

**Why:** The conditional Options validation (required only for MultipleChoice) is the most complex rule. Without it, MultipleChoice questions would be created without answer options, making them impossible to score.

---

## `UpdateQuestionRequestValidatorTests` (9 tests)

Tests FluentValidation rules for the `UpdateQuestionRequest` merge-patch DTO.

| Test                                  | What It Verifies                                   | Expected Outcome |
| ------------------------------------- | -------------------------------------------------- | ---------------- |
| `Validate_AllNullFields_Passes`       | Empty merge-patch (no-op update) passes validation | No errors        |
| `Validate_ValidPartialUpdate_Passes`  | Partial update with valid values passes            | No errors        |
| `Validate_AllValidFields_Passes`      | Full update with all valid values passes           | No errors        |
| `Validate_EmptyQuestionText_Fails`    | Non-null but empty question text is rejected       | Validation error |
| `Validate_InvalidQuestionType_Fails`  | Invalid question type is rejected                  | Validation error |
| `Validate_InvalidTiming_Fails`        | Invalid timing value is rejected                   | Validation error |
| `Validate_WeightBelowZero_Fails`      | Negative weight is rejected                        | Validation error |
| `Validate_WeightAbove100_Fails`       | Weight exceeding 100 is rejected                   | Validation error |
| `Validate_NegativeDisplayOrder_Fails` | Negative display order is rejected                 | Validation error |

**Why:** Same merge-patch guarding pattern as other update validators. Prevents partial updates from introducing invalid question types or timing values that would break the assessment flow.
