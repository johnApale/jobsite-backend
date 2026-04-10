# SharedKernel Test Coverage

← [Test Coverage](README.md)

> Tests for the foundational types that every module inherits or depends on.

---

## `EntityTests` (2 tests)

Tests the base `Entity` class that every domain entity in every module inherits from. Defects here would cascade across the entire system.

| Test                                  | What It Verifies                                                         | Expected Outcome                     |
| ------------------------------------- | ------------------------------------------------------------------------ | ------------------------------------ |
| `Entity_NewInstance_HasDefaultGuidId` | A new entity starts with `Guid.Empty` before the database assigns a UUID | `Id` equals `Guid.Empty`             |
| `Entity_SetProperties_RetainsValues`  | `Id`, `CreatedAt`, and `UpdatedAt` can be set and read back correctly    | All properties match assigned values |

**Why:** Entity is the single inheritance root for all domain objects. If property assignment or defaults break, every module's persistence layer fails silently.

---

## `AggregateRootTests` (4 tests)

Tests domain event tracking on `AggregateRoot`, which extends `Entity`. Aggregate roots collect domain events during business operations and dispatch them after `SaveChangesAsync` succeeds — this is the backbone of inter-module communication via the in-process event bus.

| Test                                                 | What It Verifies                                                            | Expected Outcome                                      |
| ---------------------------------------------------- | --------------------------------------------------------------------------- | ----------------------------------------------------- |
| `RaiseDomainEvent_SingleEvent_AppearsInDomainEvents` | Calling `RaiseDomainEvent` adds the event to the collection                 | `DomainEvents` has exactly 1 item of the correct type |
| `RaiseDomainEvent_MultipleEvents_TracksAll`          | Multiple events are accumulated, not overwritten                            | `DomainEvents` has count 3 after 3 raises             |
| `ClearDomainEvents_AfterRaising_RemovesAll`          | `ClearDomainEvents()` empties the collection (called by UoW after dispatch) | `DomainEvents` is empty                               |
| `DomainEvents_NewAggregate_IsEmpty`                  | A freshly created aggregate has no pending events                           | `DomainEvents` is empty                               |

**Why:** Domain events are the only allowed communication channel between modules. If events are lost, duplicated, or not cleared after publishing, modules will either miss critical state changes (e.g., `ApplicationSubmittedEvent` never triggers screening) or process them repeatedly.

---

## `ResultTests` (4 tests)

Tests the `Result<T>` monad used for operations that can fail without throwing exceptions. Provides railway-oriented error handling as an alternative to `AppError` exceptions.

| Test                                          | What It Verifies                                                   | Expected Outcome                                      |
| --------------------------------------------- | ------------------------------------------------------------------ | ----------------------------------------------------- |
| `Success_WithValue_IsSuccessTrue`             | `Result<T>.Success(value)` creates a successful result             | `IsSuccess` is true, `Value` matches, `Error` is null |
| `Failure_WithError_IsFailureTrue`             | `Result<T>.Failure(error)` creates a failed result                 | `IsFailure` is true, `Error.Code` matches             |
| `ImplicitConversion_FromValue_CreatesSuccess` | Assigning a raw value implicitly converts to `Result<T>.Success`   | `IsSuccess` is true, `Value` is 42                    |
| `ImplicitConversion_FromError_CreatesFailure` | Assigning an `AppError` implicitly converts to `Result<T>.Failure` | `IsFailure` is true, error code matches               |

**Why:** The implicit conversions enable clean return syntax (`return tenant;` instead of `return Result<Tenant>.Success(tenant);`). If implicit operators break, every service method using Result would need rewriting or would silently return wrong states.

---

## `AppErrorTests` (6 tests)

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

## `TenantDbContextTests` (5 tests)

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

## `IntegrationEventSerializationTests` (25 tests)

Verifies that integration events — which cross the C# → Python boundary via the message broker — serialize to snake_case JSON and round-trip without data loss. The AI Service (Python/FastAPI) deserializes these events using Pydantic, so the JSON contract must remain stable. Covers all 10 integration events (2 interview + 8 broker).

| Test                                                         | What It Verifies                                                            | Expected Outcome                                        |
| ------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------------------------------------- |
| `CandidateReadyForInterviewEvent_SerializesToSnakeCaseJson`  | All property names serialize to snake_case (`event_id`, `tenant_id`, etc.)  | JSON contains all expected snake_case keys              |
| `CandidateReadyForInterviewEvent_RoundTripsWithoutDataLoss`  | Serialize → deserialize produces identical property values                  | All fields match original                               |
| `CandidateReadyForInterviewEvent_NoPascalCaseKeysInOutput`   | PascalCase property names (`EventId`, `TenantId`) do NOT appear in JSON     | No PascalCase keys found in serialized output           |
| `InterviewCompletedEvent_SerializesToSnakeCaseJson`          | All property names serialize to snake_case including `interview_session_id` | JSON contains all expected snake_case keys              |
| `InterviewCompletedEvent_RoundTripsWithoutDataLoss`          | Serialize → deserialize preserves `OverallScore` and all GUIDs              | All fields match original                               |
| `InterviewCompletedEvent_OverallScoreSerializesAsNumber`     | `OverallScore` (int) serializes as a JSON number, not a quoted string       | JSON contains `"overall_score":75` (no quotes on value) |
| `ResumeParseRequested_SerializesToSnakeCaseJson`             | `resume_id`, `parsed_text` serialize to snake_case                          | JSON contains all expected snake_case keys              |
| `ResumeParseRequested_RoundTripsWithoutDataLoss`             | Serialize → deserialize preserves `ResumeId` and `ParsedText`               | All fields match original                               |
| `ResumeParseRequested_NoPascalCaseKeysInOutput`              | PascalCase property names do NOT appear in JSON                             | No PascalCase keys found in serialized output           |
| `ResumeParsed_SerializesToSnakeCaseJson`                     | `resume_id`, `ai_parsed_content` serialize to snake_case                    | JSON contains all expected snake_case keys              |
| `ResumeParsed_RoundTripsWithoutDataLoss`                     | Serialize → deserialize preserves `AiParsedContent`                         | All fields match original                               |
| `ScreeningEvaluationRequested_SerializesToSnakeCaseJson`     | `criteria_json`, `applicant_data_json` serialize to snake_case              | JSON contains all expected snake_case keys              |
| `ScreeningEvaluationRequested_RoundTripsWithoutDataLoss`     | Serialize → deserialize preserves `CriteriaJson` and `ApplicantDataJson`    | All fields match original                               |
| `ScreeningEvaluated_SerializesToSnakeCaseJson`               | `breakdown_json`, `overall_score` serialize to snake_case                   | JSON contains all expected snake_case keys              |
| `ScreeningEvaluated_RoundTripsWithoutDataLoss`               | Serialize → deserialize preserves `BreakdownJson` and `OverallScore`        | All fields match original                               |
| `ScreeningEvaluated_OverallScoreSerializesAsNumber`          | `OverallScore` (decimal) serializes as a JSON number                        | JSON contains `"overall_score":92.5`                    |
| `AnswerScoringRequested_SerializesToSnakeCaseJson`           | `answers_json` serializes to snake_case                                     | JSON contains all expected snake_case keys              |
| `AnswerScoringRequested_RoundTripsWithoutDataLoss`           | Serialize → deserialize preserves `AnswersJson`                             | All fields match original                               |
| `AnswersScored_SerializesToSnakeCaseJson`                    | `scores_json` serializes to snake_case                                      | JSON contains all expected snake_case keys              |
| `AnswersScored_RoundTripsWithoutDataLoss`                    | Serialize → deserialize preserves `ScoresJson`                              | All fields match original                               |
| `FeedbackGenerationRequested_SerializesToSnakeCaseJson`      | `criteria_breakdown`, `transparency_level` serialize to snake_case          | JSON contains all expected snake_case keys              |
| `FeedbackGenerationRequested_RoundTripsWithoutDataLoss`      | Serialize → deserialize preserves all fields including `OverallScore`       | All fields match original                               |
| `FeedbackGenerationRequested_OverallScoreSerializesAsNumber` | `OverallScore` (decimal) serializes as a JSON number                        | JSON contains `"overall_score":65.0`                    |
| `FeedbackGenerated_SerializesToSnakeCaseJson`                | `feedback` serializes to snake_case                                         | JSON contains all expected snake_case keys              |
| `FeedbackGenerated_RoundTripsWithoutDataLoss`                | Serialize → deserialize preserves `Feedback`                                | All fields match original                               |

**Why:** These are the contract tests for the most critical cross-service boundary in the architecture. The .NET monolith publishes events to RabbitMQ/Azure Service Bus, and the Python AI Service consumes them. If the JSON shape changes (e.g., `eventId` instead of `event_id`), the Python service will silently drop fields or crash. The `NoPascalCaseKeys` tests are a safety net against accidentally using the wrong `JsonSerializerOptions`. Companion Python tests in `ai-service/tests/test_event_contracts.py` (20 tests) validate the receiving side.
