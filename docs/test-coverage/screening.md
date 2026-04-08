# Screening Module Test Coverage

← [Test Coverage](README.md)

> Tests for the screening pipeline — deterministic scoring, question scoring, assessment, manual review, AI clients, and candidate feedback.

---

## `ScreeningConstantsTests` (45 tests)

Tests `IsValid()` methods for all 6 Screening module constant classes via `[Theory]` with `[InlineData]`. Values must match PostgreSQL CHECK constraints exactly.

| Constant Class       | Tests | What It Verifies                                                         |
| -------------------- | ----- | ------------------------------------------------------------------------ |
| `ScreeningStatus`    | 7     | Pending/InProgress/Completed/Failed valid; lowercase and unknown invalid |
| `MatchStrength`      | 7     | Strong/Good/Moderate/Weak valid; lowercase and unknown invalid           |
| `ScreeningOutcome`   | 8     | All 5 outcomes valid; lowercase and unknown invalid                      |
| `ScoreResult`        | 6     | MeetsRequirement/PartialMatch/Missing valid; others invalid              |
| `ManualReviewPolicy` | 7     | All 4 policies valid; lowercase and unknown invalid                      |
| `TransparencyLevel`  | 10    | None/Summary/Detailed valid; extensive edge-case coverage                |

**Why:** PascalCase enum values must exactly match PostgreSQL CHECK constraints. Lowercase or unknown values that pass `IsValid()` would violate DB constraints at runtime.

---

## `DeterministicScoringEngineTests` (22 tests)

Tests the core scoring algorithm that evaluates applicants against job criteria using ExactMatch, RangeMatch, and SemanticSimilarity methods. This engine determines every candidate's initial score and routing outcome.

| Test                                                                    | What It Verifies                                                        | Expected Outcome                |
| ----------------------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------- |
| `ScoreAsync_ExactMatch_SkillPresent_ReturnsFullScore`                   | Skill found in applicant data scores 100                                | Score is 100                    |
| `ScoreAsync_ExactMatch_SkillMissing_ReturnsZero`                        | Missing skill scores 0                                                  | Score is 0                      |
| `ScoreAsync_ExactMatch_CaseInsensitive_MatchesRegardlessOfCase`         | "c#" matches "C#" — case-insensitive comparison                         | Score is 100                    |
| `ScoreAsync_ExactMatch_Certification_MatchesCertName`                   | Certification category searches certifications list                     | Score is 100                    |
| `ScoreAsync_ExactMatch_Location_MatchesLocationString`                  | Location category matches applicant's location field                    | Score is 100                    |
| `ScoreAsync_ExactMatch_SkillInAiParsedContent_FoundViaSearchText`       | Keywords found in `AiParsedContent` via `SearchText`                    | Score is 100                    |
| `ScoreAsync_ExactMatch_Education_MatchesDegreeLevel`                    | Education category searches degree levels                               | Score is 100                    |
| `ScoreAsync_RangeMatch_WithExperience_ReturnsFullScore`                 | Applicant meets required years → 100                                    | Score is 100                    |
| `ScoreAsync_RangeMatch_PartialExperience_ReturnsProportionalScore`      | 3/5 years → 60                                                          | Score is 60                     |
| `ScoreAsync_RangeMatch_ZeroRequiredYears_ReturnsFullScore`              | Zero required years is always met                                       | Score is 100                    |
| `ScoreAsync_RangeMatch_NoAiParsedData_ReturnsZero`                      | No AI-parsed experience data → cannot evaluate → 0                      | Score is 0                      |
| `ScoreAsync_SemanticSimilarity_PartialOverlap_ReturnsProportionalScore` | 2/4 keywords match → proportional score                                 | Proportional percentage         |
| `ScoreAsync_SemanticSimilarity_AllKeywordsMatch_Returns100`             | All keywords match → 100                                                | Score is 100                    |
| `ScoreAsync_SemanticSimilarity_NoKeywordsMatch_ReturnsZero`             | No keywords match → 0                                                   | Score is 0                      |
| `ScoreAsync_SemanticSimilarity_EmptySearchText_ReturnsZero`             | Empty/null search text → cannot match → 0                               | Score is 0                      |
| `ScoreAsync_MultipleCriteria_WeightedAverage`                           | Multiple criteria produce correct weighted average                      | Weighted score                  |
| `ScoreAsync_WeightedAverage_VerifiesExactMath`                          | `(100*3 + 0*1) / 4 = 75` — verifies exact decimal math                  | Score is exactly 75.00          |
| `ScoreAsync_EmptyCriteria_ReturnsZero`                                  | No criteria → score is 0                                                | Score is 0                      |
| `ScoreAsync_UnknownEvaluationMethod_ReturnsZero`                        | Unknown method gracefully returns 0, not exception                      | Score is 0                      |
| `ScoreAsync_AllNullApplicantData_ProducesZeroScores`                    | All-null applicant fields → every criterion scores 0                    | Overall score is 0              |
| `ScoreAsync_BreakdownContainsReasoningAndScoreResult`                   | `CriterionBreakdown` has `Reasoning` and `ScoreResult` fields populated | Non-null reasoning/score result |
| `ScoreAsync_InvalidConfiguration_ReturnsZero`                           | Malformed criterion config → graceful 0, not exception                  | Score is 0                      |

**Why:** The deterministic scoring engine is the single most important piece of business logic in the Screening module — it produces the score that drives all routing (AutoAdvance/AutoReject/ManualReview). Incorrect math, case sensitivity bugs, or null-handling issues directly affect which candidates advance or get rejected. The weighted average math and boundary tests ensure decimal precision.

---

## `ScreeningServiceTests` (23 tests)

Tests `ScreeningService` — the orchestration service for the screening pipeline, routing, manual review, and re-scoring. Uses NSubstitute to mock all dependencies.

| Test                                                                             | What It Verifies                                                                          | Expected Outcome                                          |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| `GetResultAsync_ExistingResult_ReturnsResponse`                                  | Successful lookup maps entity to response DTO                                             | Response has matching fields                              |
| `GetResultAsync_NotFound_ThrowsAppError`                                         | Missing result throws `SCREENING_RESULT_NOT_FOUND`                                        | Throws `AppError` (404)                                   |
| `ListResultsAsync_DelegatesToRepository`                                         | Query parameters forwarded to repository                                                  | Repository called with correct params                     |
| `ManualReviewAsync_ManuallyAdvanced_UpdatesOutcomeAndStatus`                     | ManuallyAdvanced sets outcome and calls status updater with "Shortlisted"                 | Outcome updated, status updater called                    |
| `ManualReviewAsync_ManuallyRejected_UpdatesOutcomeAndStatus`                     | ManuallyRejected sets outcome and calls status updater with "Rejected"                    | Outcome updated, status updater called                    |
| `ManualReviewAsync_NotFound_ThrowsAppError`                                      | Missing result throws 404                                                                 | Throws `AppError` (404)                                   |
| `ManualReviewAsync_NotInManualReview_ThrowsUnprocessableEntity`                  | Result not in ManualReview outcome throws 422                                             | Throws `AppError` (422)                                   |
| `ProcessScreeningAsync_HighScore_AutoAdvances`                                   | Score above advance threshold → AutoAdvanced outcome                                      | `Outcome` = AutoAdvanced                                  |
| `ProcessScreeningAsync_LowScore_AutoRejects`                                     | Score below reject threshold → AutoRejected outcome                                       | `Outcome` = AutoRejected                                  |
| `ProcessScreeningAsync_HighScore_WithAfterScreeningQuestions_RoutesToAssessment` | High score + AfterScreening questions → status "Assessment" not "Shortlisted"             | Status updater called with "Assessment"                   |
| `ProcessScreeningAsync_MidScore_QueueForReview_QueuesManualReview`               | Score between thresholds + QueueForReview policy → ManualReview + Moderate match strength | `Outcome` = ManualReview, MatchStrength = Moderate        |
| `ProcessScreeningAsync_ScoreExactlyAtAdvanceThreshold_AutoAdvances`              | Score == threshold → AutoAdvanced (>= boundary)                                           | `Outcome` = AutoAdvanced                                  |
| `ProcessScreeningAsync_ScoreExactlyAtRejectThreshold_AutoRejects`                | Score == threshold → AutoRejected (<= boundary)                                           | `Outcome` = AutoRejected                                  |
| `ProcessScreeningAsync_PipelineException_SetsStatusFailed`                       | Exception during scoring → Status=Failed + FailureReason populated                        | Status = Failed, FailureReason contains message           |
| `ProcessScreeningAsync_NoApplicantData_MarksFailed`                              | Null applicant data → Status=Failed                                                       | Status = Failed                                           |
| `ProcessScreeningAsync_NotFound_ThrowsAppError`                                  | Missing screening result throws 404                                                       | Throws `AppError` (404)                                   |
| `MapToResponse_MapsAllProperties`                                                | Static mapper covers all 20+ fields                                                       | All fields match                                          |
| `RescoreApplicationAsync_CompletedResult_ResetsFieldsAndRerunsScoring`           | Completed result → resets all scoring fields to null, re-runs pipeline with new score     | Old AI/review fields null, new score applied              |
| `RescoreApplicationAsync_FailedResult_CanBeRescored`                             | Failed result → can be rescored, new score applied, FailureReason cleared                 | Status = Completed, new score applied                     |
| `RescoreApplicationAsync_PendingResult_ThrowsUnprocessable`                      | Pending result cannot be rescored                                                         | Throws `AppError` (422)                                   |
| `RescoreApplicationAsync_InProgressResult_ThrowsUnprocessable`                   | InProgress result cannot be rescored                                                      | Throws `AppError` (422)                                   |
| `RescoreApplicationAsync_NotFound_ThrowsNotFound`                                | Missing result throws 404                                                                 | Throws `AppError` (404)                                   |
| `RescoreApplicationAsync_ChangesRouting_WhenNewScoreCrossesThreshold`            | Previously AutoRejected (25) → rescores to 80 → AutoAdvanced + Shortlisted                | Outcome changes, status updater called with "Shortlisted" |

**Why:** `ScreeningService` is the central orchestrator for the entire screening pipeline. The three-tier routing tests verify the exact threshold boundary behavior (`>=` for advance, `<=` for reject) that determines whether a candidate advances, is rejected, or enters manual review. The re-scoring tests verify the complete field reset and re-evaluation flow when job criteria change, including routing change scenarios where a previously rejected candidate now qualifies.

---

## `QuestionScoringServiceTests` (15 tests)

Tests the scoring logic for screening question answers — YesNo (binary), MultipleChoice (partial credit or all-or-nothing), and FreeText (AI-delegated).

| Test                                                                            | What It Verifies                                                                                      | Expected Outcome                           |
| ------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| `ScoreResponsesAsync_YesNo_CorrectAnswer_Returns100`                            | Correct yes/no answer scores 100                                                                      | Score is 100                               |
| `ScoreResponsesAsync_YesNo_WrongAnswer_Returns0`                                | Wrong yes/no answer scores 0                                                                          | Score is 0                                 |
| `ScoreResponsesAsync_YesNo_MissingExpectedAnswer_ReturnsMissingScore`           | Missing expected answer config → score result is Missing                                              | ScoreResult = Missing                      |
| `ScoreResponsesAsync_YesNo_AppliesScoreToResponseEntity`                        | Score is mutated onto the `ScreeningQuestionResponse` entity (Score, ScoreResult, ScoredAt)           | Entity fields populated                    |
| `ScoreResponsesAsync_MultipleChoice_AllCorrect_Returns100`                      | All correct options selected → 100                                                                    | Score is 100                               |
| `ScoreResponsesAsync_MultipleChoice_PartialCredit_ReturnsProportional`          | 2/3 correct → proportional score                                                                      | Score is ~66.67                            |
| `ScoreResponsesAsync_MultipleChoice_PartialCreditWithIncorrect_PenalizesScore`  | 2 correct - 1 incorrect out of 3 → `(2-1)/3*100 = 33.33`                                              | Score is ~33.33                            |
| `ScoreResponsesAsync_MultipleChoice_AllOrNothing_WrongAnswer_ReturnsZero`       | All-or-nothing grading: any wrong → 0                                                                 | Score is 0                                 |
| `ScoreResponsesAsync_MultipleChoice_NoneSelected_ReturnsZero`                   | No options selected → 0                                                                               | Score is 0                                 |
| `ScoreResponsesAsync_MultipleChoice_MalformedJson_ReturnsMissingScore`          | Invalid JSON in response data → graceful Missing result, not exception                                | ScoreResult = Missing                      |
| `ScoreResponsesAsync_FreeText_DelegatesToAiClient`                              | Free text answers delegate scoring to AI answer scoring client                                        | AI client called                           |
| `ScoreResponsesAsync_FreeText_AiUnavailable_ReturnsEmpty`                       | AI unavailable → returns empty scores for free text                                                   | Returns empty list                         |
| `ScoreResponsesAsync_FreeText_AiScoresAppliedToResponseEntity`                  | AI-returned scores are applied back to response entity (Score, ScoreResult, ScoreReasoning, ScoredAt) | All entity fields populated from AI result |
| `ScoreResponsesAsync_MixedTypes_ScoresAllDeterministicallyAndDelegatesFreeText` | Batch with YesNo + MC + FreeText: deterministic types scored locally, FreeText delegated              | All types scored, AI called for FreeText   |
| `ScoreResponsesAsync_UnknownQuestion_IsSkipped`                                 | Response for unknown question ID is silently skipped                                                  | No exception, other responses scored       |

**Why:** Question scoring directly impacts assessment outcomes and candidate routing. The partial credit math (with penalty for incorrect selections), all-or-nothing grading, and graceful AI fallback are all critical scoring paths that affect candidate outcomes.

---

## `ApplicationSubmittedScreeningHandlerTests` (7 tests)

Tests the domain event handler that creates a `ScreeningResult` and triggers the screening pipeline when an application is submitted.

| Test                                                                            | What It Verifies                                                                                              | Expected Outcome                                  |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| `Handle_CreatesScreeningResultWithPendingStatus`                                | Handler creates a `ScreeningResult` entity with `Pending` status                                              | Result added to repository with Pending status    |
| `Handle_WithQuestionAnswers_StoresAllResponses`                                 | At-application question answers are stored as `ScreeningQuestionResponse` entities with correct field mapping | Responses added with correct QuestionId/Text/Data |
| `Handle_NoQuestionAnswers_StillCreatesResultAndRunsPipeline`                    | Empty answers still creates the result and runs the pipeline                                                  | Result created, ProcessScreeningAsync called      |
| `Handle_CallsProcessScreeningAsync`                                             | Handler delegates to `IScreeningService.ProcessScreeningAsync`                                                | Service method called with correct IDs            |
| `Handle_CompletedWithAutoAdvanced_PublishesCvScreeningCompletedWithPassedTrue`  | AutoAdvanced result → publishes event with `PassedScreening = true`                                           | Event published with PassedScreening = true       |
| `Handle_CompletedWithAutoRejected_PublishesCvScreeningCompletedWithPassedFalse` | AutoRejected result → publishes event with `PassedScreening = false`                                          | Event published with PassedScreening = false      |
| `Handle_ScreeningFailed_DoesNotPublishEvent`                                    | Failed screening → no event published                                                                         | Event NOT published                               |

**Why:** This handler is the entry point to the screening pipeline — triggered by the Recruitment module's `ApplicationSubmittedEvent`. If the handler fails to create the result, store answers, or trigger the pipeline, screening never starts. The event publication tests verify correct integration event data for downstream consumers.

---

## `AssessmentServiceTests` (9 tests)

Tests the `AssessmentService` for AfterScreening question submission, scoring, and completion flow.

| Test                                                                  | What It Verifies                                                                    | Expected Outcome                              |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | --------------------------------------------- |
| `SubmitAssessmentAsync_ValidAnswers_CalculatesAverageAssessmentScore` | Assessment score = average of individual answer scores: `(100+100+0)/3 ≈ 66.67`     | AssessmentScore is ~66.67                     |
| `SubmitAssessmentAsync_AutoAdvancePolicy_UpdatesStatusToShortlisted`  | AutoAdvance policy → status updated to "Shortlisted"                                | Status updater called with "Shortlisted"      |
| `SubmitAssessmentAsync_PublishesAssessmentCompletedEvent`             | Publishes `AssessmentCompletedEvent` with correct ApplicationId, Score, CompletedAt | Event published with matching fields          |
| `SubmitAssessmentAsync_AlreadySubmitted_ThrowsConflict`               | Re-submission throws 409 Conflict                                                   | Throws `AppError` (409)                       |
| `SubmitAssessmentAsync_ResultNotFound_Throws404`                      | Missing screening result throws 404                                                 | Throws `AppError` (404)                       |
| `SubmitAssessmentAsync_DuplicateAnswer_SkipsExistingResponse`         | Duplicate question answer is silently skipped                                       | No exception, other answers processed         |
| `GetAssessmentStatusAsync_NotSubmitted_ReturnsPendingWithQuestions`   | Pending assessment returns status with question list                                | `IsSubmitted` = false, Questions populated    |
| `GetAssessmentStatusAsync_AlreadySubmitted_ReturnsSubmittedWithScore` | Submitted assessment returns status with score                                      | `IsSubmitted` = true, AssessmentScore present |
| `GetAssessmentStatusAsync_NoAfterScreeningQuestions_Throws422`        | Job with no AfterScreening questions → 422 (assessment not applicable)              | Throws `AppError` (422)                       |

**Why:** Assessment is the optional second phase after initial screening. The average score calculation, policy-driven routing, and event publication are critical for candidates who pass initial screening and need to complete additional questions.

---

## `AiScoringClientTests` (3 tests)

Tests the `AiScoringClient` HTTP client that calls the AI Service for criteria-based scoring. Uses a mock `HttpMessageHandler`.

| Test                                               | What It Verifies                                  | Expected Outcome |
| -------------------------------------------------- | ------------------------------------------------- | ---------------- |
| `EvaluateAsync_SuccessResponse_DeserializesResult` | 200 OK → correctly deserialized `AiScoringResult` | Non-null result  |
| `EvaluateAsync_ServerError_ReturnsNull`            | 500 → returns null (graceful degradation)         | Returns null     |
| `EvaluateAsync_NetworkError_ReturnsNull`           | Connection refused → returns null                 | Returns null     |

---

## `AiAnswerScoringClientTests` (3 tests)

Tests the `AiAnswerScoringClient` HTTP client for AI-powered free-text answer scoring.

| Test                                                   | What It Verifies                              | Expected Outcome |
| ------------------------------------------------------ | --------------------------------------------- | ---------------- |
| `ScoreAnswersAsync_SuccessResponse_DeserializesScores` | 200 OK → correctly deserialized answer scores | Non-null result  |
| `ScoreAnswersAsync_ServerError_ReturnsNull`            | 500 → returns null                            | Returns null     |
| `ScoreAnswersAsync_NetworkError_ReturnsNull`           | Connection refused → returns null             | Returns null     |

---

## `AiCandidateFeedbackClientTests` (3 tests)

Tests the `AiCandidateFeedbackClient` HTTP client for generating candidate transparency feedback.

| Test                                                          | What It Verifies                  | Expected Outcome |
| ------------------------------------------------------------- | --------------------------------- | ---------------- |
| `GenerateFeedbackAsync_SuccessResponse_ReturnsFeedbackString` | 200 OK → returns feedback string  | Non-null string  |
| `GenerateFeedbackAsync_ServerError_ReturnsNull`               | 500 → returns null                | Returns null     |
| `GenerateFeedbackAsync_NetworkError_ReturnsNull`              | Connection refused → returns null | Returns null     |

**Why (all 3 AI clients):** All AI HTTP clients follow the same resilience pattern — success returns deserialized data, any failure returns null. This ensures the screening pipeline never crashes due to AI Service unavailability. The null returns trigger graceful fallback paths in the calling services.

---

## `CandidateFeedbackServiceTests` (4 tests)

Tests the `CandidateFeedbackService` wrapper that delegates to the AI client for candidate transparency feedback generation.

| Test                                                              | What It Verifies                                             | Expected Outcome         |
| ----------------------------------------------------------------- | ------------------------------------------------------------ | ------------------------ |
| `GenerateFeedbackAsync_AiReturnsFeedback_ReturnsString`           | AI returns feedback → returns string to caller               | Non-null feedback string |
| `GenerateFeedbackAsync_AiUnavailable_ReturnsNullWithoutException` | AI unavailable → returns null gracefully                     | Returns null, no throw   |
| `GenerateFeedbackAsync_ForwardsCorrectTransparencyLevel`          | Transparency level from tenant config forwarded to AI client | Client called with level |
| `GenerateFeedbackAsync_ForwardsBreakdownAndScore`                 | Criteria breakdown and overall score forwarded correctly     | Client called with data  |

**Why:** Candidate feedback is a tenant-configurable transparency feature. The service must forward the correct transparency level (Summary vs Detailed) and handle AI unavailability gracefully.

---

## `ScreeningValidatorTests` (10 tests)

Tests FluentValidation rules for `SubmitAssessmentRequest` and `ManualReviewRequest` DTOs.

| Test                                                               | What It Verifies                                         | Expected Outcome       |
| ------------------------------------------------------------------ | -------------------------------------------------------- | ---------------------- |
| `SubmitAssessmentRequestValidator_EmptyAnswers_Fails`              | Empty answers list is rejected                           | Error on `Answers`     |
| `SubmitAssessmentRequestValidator_AnswerWithEmptyQuestionId_Fails` | Answer with `Guid.Empty` question ID is rejected         | Error on `QuestionId`  |
| `SubmitAssessmentRequestValidator_ValidRequest_Passes`             | Valid request with one answer passes                     | `IsValid` is true      |
| `SubmitAssessmentRequestValidator_MultipleValidAnswers_Passes`     | Valid request with multiple answers (text + JSON) passes | `IsValid` is true      |
| `ManualReviewRequestValidator_EmptyOutcome_Fails`                  | Empty outcome is rejected                                | Error on `Outcome`     |
| `ManualReviewRequestValidator_InvalidOutcome_Fails`                | "AutoAdvanced" rejected (not a manual review outcome)    | Error on `Outcome`     |
| `ManualReviewRequestValidator_ValidManuallyAdvanced_Passes`        | "ManuallyAdvanced" passes                                | `IsValid` is true      |
| `ManualReviewRequestValidator_ManuallyRejected_Passes`             | "ManuallyRejected" passes                                | `IsValid` is true      |
| `ManualReviewRequestValidator_ReviewNotesTooLong_Fails`            | Notes over 2000 chars rejected                           | Error on `ReviewNotes` |
| `ManualReviewRequestValidator_ReviewNotesExactly2000Chars_Passes`  | Notes at exactly 2000 chars passes (boundary test)       | `IsValid` is true      |

**Why:** Input validation is the first line of defense. The boundary test for review notes (exactly 2000 chars) validates the `<=` vs `<` boundary. The outcome validator ensures only manual review outcomes (ManuallyAdvanced/ManuallyRejected) are accepted — not automated outcomes like AutoAdvanced.

---

## AI Service Contract Tests (WireMock)

Contract tests verify that the .NET HTTP clients send the correct request bodies (snake_case field names, correct endpoint paths, POST method) and correctly deserialize AI Service responses. Tests run against a real HTTP server via WireMock — unlike unit tests that mock the `HttpMessageHandler`, these exercise the full HTTP stack.

### `AiScoringContractTests` (4 tests)

| Test                                                         | What It Verifies                                            | Expected Outcome  |
| ------------------------------------------------------------ | ----------------------------------------------------------- | ----------------- |
| `EvaluateAsync_SendsCorrectRequestBody_WithSnakeCaseFields`  | Request has `criteria` + `applicant` with snake_case fields | Correct JSON body |
| `EvaluateAsync_EmptyBreakdown_DeserializesCorrectly`         | Empty breakdown array + zero score deserializes             | Empty list, 0     |
| `EvaluateAsync_ServerReturns500_ReturnsNull`                 | 500 → returns null over real HTTP                           | Returns null      |
| `EvaluateAsync_MalformedJson_ReturnsNull`                    | Invalid JSON body → returns null (catches JsonException)    | Returns null      |

### `AiAnswerScoringContractTests` (4 tests)

| Test                                                                | What It Verifies                     | Expected Outcome  |
| ------------------------------------------------------------------- | ------------------------------------ | ----------------- |
| `ScoreAnswersAsync_SendsCorrectRequestBody_WithSnakeCaseFields`     | Request has `answers` snake_case     | Correct JSON body |
| `ScoreAnswersAsync_MultipleAnswers_DeserializesAll`                 | Multiple answer scores deserialized  | 2 scores returned |
| `ScoreAnswersAsync_ServerReturns500_ReturnsNull`                    | 500 → returns null                   | Returns null      |
| `ScoreAnswersAsync_MalformedJson_ReturnsNull`                       | Invalid JSON → returns null          | Returns null      |

### `AiCandidateFeedbackContractTests` (4 tests)

| Test                                                                    | What It Verifies                                                     | Expected Outcome |
| ----------------------------------------------------------------------- | -------------------------------------------------------------------- | ---------------- |
| `GenerateFeedbackAsync_SendsCorrectRequestBody_WithSnakeCaseFields`     | Request has `criteria_breakdown`, `overall_score`, `transparency_level` | Correct body  |
| `GenerateFeedbackAsync_SummaryLevel_SendsCorrectLevel`                  | "Summary" transparency level sent correctly                          | Level verified   |
| `GenerateFeedbackAsync_NullFeedbackField_ReturnsNull`                   | `{"feedback": null}` → returns null                                  | Returns null     |
| `GenerateFeedbackAsync_ServerReturns500_ReturnsNull`                    | 500 → returns null                                                   | Returns null     |

### `AiResumeParserContractTests` (4 tests)

| Test                                                       | What It Verifies                                               | Expected Outcome |
| ---------------------------------------------------------- | -------------------------------------------------------------- | ---------------- |
| `ParseAsync_SendsCorrectRequestBody_WithSnakeCaseFields`   | Request has `parsed_text`, response has skills/experience/etc. | Full shape       |
| `ParseAsync_MinimalResponse_DeserializesNullableFields`     | All-null response fields deserialize correctly                 | All null         |
| `ParseAsync_ServerReturns500_ReturnsNull`                   | 500 → returns null                                             | Returns null     |
| `ParseAsync_MalformedJson_ReturnsNull`                      | Invalid JSON → returns null                                    | Returns null     |

### `AiCriteriaSuggesterContractTests` (4 tests)

| Test                                                      | What It Verifies                            | Expected Outcome |
| --------------------------------------------------------- | ------------------------------------------- | ---------------- |
| `SuggestAsync_SendsCorrectRequestBody_WithSnakeCaseFields` | Request has `job_title` + `job_description` | Correct body     |
| `SuggestAsync_EmptyList_DeserializesCorrectly`             | Empty array response → empty list           | Empty list       |
| `SuggestAsync_ServerReturns500_ReturnsNull`                | 500 → returns null                          | Returns null     |
| `SuggestAsync_MalformedJson_ReturnsNull`                   | Invalid JSON → returns null                 | Returns null     |

### `AiQuestionSuggesterContractTests` (4 tests)

| Test                                                       | What It Verifies                                 | Expected Outcome |
| ---------------------------------------------------------- | ------------------------------------------------ | ---------------- |
| `SuggestAsync_SendsCorrectRequestBody_WithCriteriaContext`  | Request has `job_description` + `criteria` array | Correct body     |
| `SuggestAsync_EmptyList_DeserializesCorrectly`              | Empty array response → empty list                | Empty list       |
| `SuggestAsync_ServerReturns500_ReturnsNull`                 | 500 → returns null                               | Returns null     |
| `SuggestAsync_MalformedJson_ReturnsNull`                    | Invalid JSON → returns null                      | Returns null     |

**Why:** Unit tests with `MockHttpMessageHandler` only test deserialization and error handling — they don't verify what the client actually sends over the wire. Contract tests with WireMock capture and assert the exact request body (proving snake_case serialization works end-to-end), verify the correct endpoint path is called, and exercise the full HTTP pipeline including content-type negotiation.

---

## E2E Screening Pipeline Tests

### `ScreeningPipelineTests` (10 tests)

End-to-end tests exercising the full screening pipeline with real PostgreSQL persistence and real `DeterministicScoringEngine`. Cross-module readers and AI clients are stubbed. Tests verify the complete flow from score calculation through three-tier routing to persisted results.

| Test                                                             | What It Verifies                                        | Expected Outcome                    |
| ---------------------------------------------------------------- | ------------------------------------------------------- | ----------------------------------- |
| `ProcessScreening_HighScore_AutoAdvancesApplication`             | Score ≥ threshold → AutoAdvanced, status "Shortlisted"  | Outcome=AutoAdvanced, score=100     |
| `ProcessScreening_LowScore_AutoRejectsApplication`              | Score ≤ threshold → AutoRejected, status "Rejected"     | Outcome=AutoRejected, score=0       |
| `ProcessScreening_MiddleScore_RoutesToManualReview`              | Score between thresholds + QueueForReview → ManualReview | Outcome=ManualReview, score=50      |
| `ProcessScreening_AiScoringEnabled_PopulatesAiFields`            | AI enabled + successful → AiOverallScore and breakdown  | Both deterministic and AI populated |
| `ProcessScreening_AiScoringUnavailable_FallsBackToDeterministic` | AI enabled but returns null → deterministic score only  | AI fields null, deterministic works |
| `ProcessScreening_TransparencyEnabled_PopulatesCandidateFeedback` | Transparency enabled → CandidateFeedback populated      | Feedback text persisted             |
| `ProcessScreening_NoApplicantData_SetsStatusFailed`              | Null applicant data → Status=Failed + FailureReason     | Status=Failed, no score             |
| `ProcessScreening_HasAfterScreeningQuestions_RoutesToAssessment`  | High score + AfterScreening questions → "Assessment"    | Routed to Assessment                |
| `ProcessScreening_AutoAdvanceAllPolicy_MiddleScoreAutoAdvances`  | Middle score + AutoAdvanceAll policy → AutoAdvanced      | AutoAdvanced despite 50 score       |
| `ProcessScreening_ScoringBreakdown_IsValidSerializedJson`        | CriteriaScoreBreakdown is valid deserializable JSON     | 2 criteria in breakdown, valid JSON |

**Why:** Unit tests for `ScreeningService` mock every dependency, so they can't catch persistence issues, EF Core transaction behavior, or real scoring math integration. These E2E tests wire the real `DeterministicScoringEngine` + real PostgreSQL repositories + real `ScreeningService` together, proving the full pipeline produces correct persisted results. The transparency feedback test uncovered a real column type bug (`jsonb` → `text`) where the AI Service returns plain text, not structured JSON.
