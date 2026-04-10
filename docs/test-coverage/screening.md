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

Tests the scoring logic for screening question answers — YesNo (binary), MultipleChoice (partial credit or all-or-nothing), and FreeText (queued for async AI scoring via message broker).

| Test                                                                          | What It Verifies                                                                            | Expected Outcome                                     |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| `ScoreResponses_YesNo_CorrectAnswer_Returns100`                               | Correct yes/no answer scores 100                                                            | Score is 100                                         |
| `ScoreResponses_YesNo_WrongAnswer_Returns0`                                   | Wrong yes/no answer scores 0                                                                | Score is 0                                           |
| `ScoreResponses_YesNo_MissingExpectedAnswer_ReturnsMissingScore`              | Missing expected answer config → score result is Missing                                    | ScoreResult = Missing                                |
| `ScoreResponses_YesNo_AppliesScoreToResponseEntity`                           | Score is mutated onto the `ScreeningQuestionResponse` entity (Score, ScoreResult, ScoredAt) | Entity fields populated                              |
| `ScoreResponses_MultipleChoice_AllCorrect_Returns100`                         | All correct options selected → 100                                                          | Score is 100                                         |
| `ScoreResponses_MultipleChoice_PartialCredit_ReturnsProportional`             | 2/3 correct → proportional score                                                            | Score is ~66.67                                      |
| `ScoreResponses_MultipleChoice_PartialCreditWithIncorrect_PenalizesScore`     | 2 correct - 1 incorrect out of 3 → `(2-1)/3*100 = 33.33`                                    | Score is ~33.33                                      |
| `ScoreResponses_MultipleChoice_AllOrNothing_WrongAnswer_ReturnsZero`          | All-or-nothing grading: any wrong → 0                                                       | Score is 0                                           |
| `ScoreResponses_MultipleChoice_NoneSelected_ReturnsZero`                      | No options selected → 0                                                                     | Score is 0                                           |
| `ScoreResponses_MultipleChoice_MalformedJson_ReturnsMissingScore`             | Invalid JSON in response data → graceful Missing result, not exception                      | ScoreResult = Missing                                |
| `ScoreResponses_FreeText_ReturnsPendingAiRequest`                             | Free text answers return `AnswerScoringRequest` for async AI scoring via broker             | Returns pending request, not scored locally          |
| `ScoreResponses_FreeText_NoPendingWhenNoFreeText`                             | Batch with no FreeText produces no pending AI requests                                      | Empty pending list                                   |
| `ScoreResponses_FreeText_BuildsRequestWithGuidanceAndTopics`                  | FreeText request includes scoring guidance and topic context                                | Request has guidance and topics populated             |
| `ScoreResponses_MixedTypes_ScoresDeterministicAndQueuesFreeText`              | Batch with YesNo + MC + FreeText: deterministic scored locally, FreeText queued for broker   | Deterministic scored, FreeText in pending list       |
| `ScoreResponses_UnknownQuestion_IsSkipped`                                    | Response for unknown question ID is silently skipped                                        | No exception, other responses scored                 |

**Why:** Question scoring directly impacts assessment outcomes and candidate routing. The partial credit math (with penalty for incorrect selections), all-or-nothing grading, and async AI delegation via broker are all critical scoring paths that affect candidate outcomes. FreeText answers are queued as `AnswerScoringRequest` objects and published to the message broker for async AI scoring — the `AnswersScoredConsumer` applies the results when the AI Service responds.

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

Contract tests verify that the .NET HTTP clients send the correct request bodies (snake_case field names, correct endpoint paths, POST method) and correctly deserialize AI Service responses. Tests run against a real HTTP server via WireMock — unlike unit tests that mock the `HttpMessageHandler`, these exercise the full HTTP stack. Only the 2 synchronous HTTP clients remain; the 4 async operations (resume parsing, screening evaluation, answer scoring, candidate feedback) now use the message broker.

### `AiCriteriaSuggesterContractTests` (4 tests)

| Test                                                       | What It Verifies                            | Expected Outcome |
| ---------------------------------------------------------- | ------------------------------------------- | ---------------- |
| `SuggestAsync_SendsCorrectRequestBody_WithSnakeCaseFields` | Request has `job_title` + `job_description` | Correct body     |
| `SuggestAsync_EmptyList_DeserializesCorrectly`             | Empty array response → empty list           | Empty list       |
| `SuggestAsync_ServerReturns500_ReturnsNull`                | 500 → returns null                          | Returns null     |
| `SuggestAsync_MalformedJson_ReturnsNull`                   | Invalid JSON → returns null                 | Returns null     |

### `AiQuestionSuggesterContractTests` (4 tests)

| Test                                                       | What It Verifies                                 | Expected Outcome |
| ---------------------------------------------------------- | ------------------------------------------------ | ---------------- |
| `SuggestAsync_SendsCorrectRequestBody_WithCriteriaContext` | Request has `job_description` + `criteria` array | Correct body     |
| `SuggestAsync_EmptyList_DeserializesCorrectly`             | Empty array response → empty list                | Empty list       |
| `SuggestAsync_ServerReturns500_ReturnsNull`                | 500 → returns null                               | Returns null     |
| `SuggestAsync_MalformedJson_ReturnsNull`                   | Invalid JSON → returns null                      | Returns null     |

**Why:** Unit tests with `MockHttpMessageHandler` only test deserialization and error handling — they don't verify what the client actually sends over the wire. Contract tests with WireMock capture and assert the exact request body (proving snake_case serialization works end-to-end), verify the correct endpoint path is called, and exercise the full HTTP pipeline including content-type negotiation. Only the 2 synchronous HTTP clients (`AiCriteriaSuggesterClient`, `AiQuestionSuggesterClient`) require contract tests — the 4 async operations now use RabbitMQ and are tested via consumer unit tests.

---

## E2E Screening Pipeline Tests

### `ScreeningPipelineTests` (10 tests)

End-to-end tests exercising the full screening pipeline with real PostgreSQL persistence and real `DeterministicScoringEngine`. Cross-module readers are stubbed. AI operations use `IEventPublisher` to publish events to the message broker — consumer response handling is tested separately. Tests verify the complete flow from score calculation through three-tier routing to persisted results.

| Test                                                                    | What It Verifies                                                            | Expected Outcome                                     |
| ----------------------------------------------------------------------- | --------------------------------------------------------------------------- | ---------------------------------------------------- |
| `ProcessScreening_HighScore_AutoAdvancesApplication`                    | Score ≥ threshold → AutoAdvanced, status "Shortlisted"                      | Outcome=AutoAdvanced, score=100                      |
| `ProcessScreening_LowScore_AutoRejectsApplication`                      | Score ≤ threshold → AutoRejected, status "Rejected"                         | Outcome=AutoRejected, score=0                        |
| `ProcessScreening_MiddleScore_RoutesToManualReview`                     | Score between thresholds + QueueForReview → ManualReview                    | Outcome=ManualReview, score=50                       |
| `ProcessScreening_AiScoringEnabled_PublishesScreeningEvaluationEvent`   | AI enabled → publishes `ScreeningEvaluationRequested` event to broker       | Event published with correct data                    |
| `ProcessScreening_AiScoringDisabled_DoesNotPublishEvaluationEvent`      | AI disabled → no evaluation event published                                 | No event published                                   |
| `ProcessScreening_TransparencyEnabled_PublishesFeedbackEvent`           | Transparency enabled → publishes `FeedbackGenerationRequested` event        | Feedback event published                             |
| `ProcessScreening_NoApplicantData_SetsStatusFailed`                     | Null applicant data → Status=Failed + FailureReason                         | Status=Failed, no score                              |
| `ProcessScreening_HasAfterScreeningQuestions_RoutesToAssessment`        | High score + AfterScreening questions → "Assessment"                        | Routed to Assessment                                 |
| `ProcessScreening_AutoAdvanceAllPolicy_MiddleScoreAutoAdvances`         | Middle score + AutoAdvanceAll policy → AutoAdvanced                         | AutoAdvanced despite 50 score                        |
| `ProcessScreening_ScoringBreakdown_IsValidSerializedJson`               | CriteriaScoreBreakdown is valid deserializable JSON                         | 2 criteria in breakdown, valid JSON                  |

**Why:** Unit tests for `ScreeningService` mock every dependency, so they can't catch persistence issues, EF Core transaction behavior, or real scoring math integration. These E2E tests wire the real `DeterministicScoringEngine` + real PostgreSQL repositories + real `ScreeningService` together, proving the full pipeline produces correct persisted results. AI operations are now asynchronous — the pipeline publishes events to the broker and the results arrive later via consumers.

---

## Message Broker Consumer Tests

### `ScreeningEvaluatedConsumerTests` (3 tests)

Tests the `ScreeningEvaluatedConsumer` that processes `ScreeningEvaluated` events from the AI Service, applying AI scoring results to screening records.

| Test                                                    | What It Verifies                                                | Expected Outcome                        |
| ------------------------------------------------------- | --------------------------------------------------------------- | --------------------------------------- |
| `Consume_ValidEvent_UpdatesAiFields`                    | AI overall score and breakdown stored on screening result       | AiOverallScore and breakdown populated  |
| `Consume_ValidEvent_PreservesDeterministicScore`        | AI update does not overwrite deterministic scoring fields       | Deterministic score unchanged           |
| `Consume_ResultNotFound_DoesNotThrow`                   | Missing screening result handled gracefully                     | No exception, logs warning              |

### `AnswersScoredConsumerTests` (3 tests)

Tests the `AnswersScoredConsumer` that processes `AnswersScored` events from the AI Service, applying AI scoring to free-text question responses.

| Test                                                    | What It Verifies                                                | Expected Outcome                        |
| ------------------------------------------------------- | --------------------------------------------------------------- | --------------------------------------- |
| `Consume_ValidScores_UpdatesResponseRecords`            | AI scores applied to matching question response records         | Score, ScoreResult, ScoreReasoning set  |
| `Consume_EmptyScoresJson_DoesNotThrow`                  | Empty scores array handled gracefully                           | No exception                            |
| `Consume_UnmatchedQuestionId_SkipsGracefully`           | Score for non-existent question ID skipped                      | No exception, other scores applied      |

### `FeedbackGeneratedConsumerTests` (3 tests)

Tests the `FeedbackGeneratedConsumer` that processes `FeedbackGenerated` events from the AI Service, storing candidate transparency feedback.

| Test                                                    | What It Verifies                                                | Expected Outcome                        |
| ------------------------------------------------------- | --------------------------------------------------------------- | --------------------------------------- |
| `Consume_ValidEvent_SetsCandidateFeedback`              | Feedback text stored on screening result                        | CandidateFeedback populated             |
| `Consume_ValidEvent_PreservesExistingFields`            | Feedback update does not overwrite other screening fields       | Other fields unchanged                  |
| `Consume_ResultNotFound_DoesNotThrow`                   | Missing screening result handled gracefully                     | No exception, logs warning              |

**Why:** These consumers are the receiving side of the async AI pipeline. Each consumer must correctly parse broker messages, apply results to the right database records, and handle edge cases (missing records, empty payloads) without crashing. The "preserves existing fields" tests ensure consumers only update their specific fields.
