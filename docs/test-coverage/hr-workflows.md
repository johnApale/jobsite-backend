# HR Workflows Module Test Coverage

← [Test Coverage](README.md)

> Tests for final interviews, panel feedback aggregation, job offer lifecycle, and event handler integration.

---

## `InterviewServiceTests` (16 tests)

Tests `InterviewService` — scheduling interviews, submitting panelist feedback, recording decisions, and managing interview cancellations. Uses NSubstitute for repository, unit of work, event dispatcher, and feedback aggregation service mocks.

| Test                                                             | What It Verifies                                                        | Expected Outcome                               |
| ---------------------------------------------------------------- | ----------------------------------------------------------------------- | ---------------------------------------------- |
| `ScheduleInterview_NewApplication_CreatesInterviewWithPanelists` | Creates FinalInterview + InterviewPanelists, dispatches event           | Interview created, panelists added, event sent |
| `ScheduleInterview_AlreadyExists_ThrowsConflict`                 | Duplicate scheduling throws `INTERVIEW_ALREADY_EXISTS`                  | Throws `AppError` (409)                        |
| `GetInterview_Exists_ReturnsResponse`                            | Successful lookup maps entity to response DTO with panelists            | Response has all mapped fields                 |
| `GetInterview_NotFound_Throws404`                                | Missing interview throws `INTERVIEW_NOT_FOUND`                          | Throws `AppError` (404)                        |
| `UpdateInterview_Scheduled_UpdatesFields`                        | Updates type, time, duration, location on scheduled interview           | Fields updated correctly                       |
| `UpdateInterview_Completed_ThrowsConflict`                       | Cannot update completed interview                                       | Throws `AppError` (409)                        |
| `SubmitFeedback_ValidPanelist_SetsFeedbackFields`                | Sets rating, recommendation, strengths, concerns, notes, timestamp      | Feedback fields populated                      |
| `SubmitFeedback_AlreadySubmitted_ThrowsConflict`                 | Duplicate feedback submission throws `FEEDBACK_ALREADY_SUBMITTED`       | Throws `AppError` (409)                        |
| `SubmitFeedback_PanelistNotFound_Throws404`                      | Non-existent panelist throws `PANELIST_NOT_FOUND`                       | Throws `AppError` (404)                        |
| `SubmitFeedback_AllPanelistsDone_AutoCompletesInterview`         | When all panelists submit, status → Completed with aggregated result    | Auto-completion trigger                        |
| `RecordDecision_PositiveRecommendation_SetsDecisionFields`       | Hire/StrongHire sets OverallRecommendation, DecidedBy, DecidedAt        | Decision recorded correctly                    |
| `RecordDecision_NegativeRecommendation_RejectsApplication`       | NoHire/StrongNoHire publishes `ApplicationStatusChangedEvent(Rejected)` | Application rejected                           |
| `CancelInterview_Scheduled_SetsCancelledStatus`                  | Cancelling sets status, timestamp, and reason                           | Status → Cancelled                             |
| `CancelInterview_AlreadyCompleted_ThrowsConflict`                | Cannot cancel completed interview                                       | Throws `AppError` (409)                        |
| `CancelInterview_AlreadyCancelled_ThrowsConflict`                | Cannot cancel already-cancelled interview                               | Throws `AppError` (409)                        |
| `CancelInterview_NotFound_Throws404`                             | Missing interview throws `INTERVIEW_NOT_FOUND`                          | Throws `AppError` (404)                        |

**Why:** Interview scheduling is a critical workflow that involves creating related panelist records, dispatching domain events, and updating application status. These tests verify the full lifecycle: schedule → feedback → decision → completion/cancellation. Auto-completion on last panelist feedback is a key business rule that prevents interviews from getting stuck.

---

## `OfferServiceTests` (13 tests)

Tests `OfferService` — creating draft offers, updating them, extending to candidates, handling responses (accept/decline), and withdrawals. Uses NSubstitute for repository, unit of work, and event dispatcher mocks.

| Test                                                | What It Verifies                                               | Expected Outcome                  |
| --------------------------------------------------- | -------------------------------------------------------------- | --------------------------------- |
| `CreateOffer_NewApplication_CreatesDraftOffer`      | Creates offer in Draft status with all fields mapped           | Offer created with Draft status   |
| `CreateOffer_AlreadyExists_ThrowsConflict`          | Duplicate offer throws `OFFER_ALREADY_EXISTS`                  | Throws `AppError` (409)           |
| `GetOffer_Exists_ReturnsResponse`                   | Successful lookup maps entity to response DTO                  | Response has all mapped fields    |
| `GetOffer_NotFound_Throws404`                       | Missing offer throws `OFFER_NOT_FOUND`                         | Throws `AppError` (404)           |
| `UpdateOffer_Draft_UpdatesFields`                   | Updates salary, currency, period, type, etc. on draft offer    | Fields updated correctly          |
| `UpdateOffer_NotDraft_ThrowsConflict`               | Cannot update non-draft offer                                  | Throws `AppError` (409)           |
| `ExtendOffer_Draft_MovesPendingAndPublishesEvent`   | Transitions Draft → Pending, sets ExtendedAt, dispatches event | Status → Pending, event published |
| `ExtendOffer_NotDraft_ThrowsConflict`               | Cannot extend non-draft offer                                  | Throws `AppError` (409)           |
| `RespondToOffer_Accept_MovesToAcceptedAndHired`     | Accept: Status → Accepted, application status → Hired          | Correct status transitions        |
| `RespondToOffer_Decline_MovesToDeclinedAndRejected` | Decline: Status → Declined, application status → Rejected      | Correct status transitions        |
| `RespondToOffer_NotPending_ThrowsConflict`          | Cannot respond to non-pending offer                            | Throws `AppError` (409)           |
| `WithdrawOffer_DraftOrPending_SetsWithdrawn`        | Withdraw sets status, timestamp, and reason                    | Status → Withdrawn                |
| `WithdrawOffer_AlreadyAccepted_ThrowsConflict`      | Cannot withdraw accepted offer                                 | Throws `AppError` (409)           |

**Why:** The offer lifecycle has strict state machine rules (Draft → Pending → Accepted/Declined, with Withdrawn as an escape from Draft/Pending). Each transition publishes domain events and may update the application status across module boundaries. These tests verify that invalid transitions are rejected and that the correct events propagate.

---

## `FeedbackAggregationServiceTests` (5 tests)

Tests `FeedbackAggregationService` — computing the overall interview recommendation from individual panelist recommendations using majority vote logic.

| Test                                                                   | What It Verifies                                     | Expected Outcome       |
| ---------------------------------------------------------------------- | ---------------------------------------------------- | ---------------------- |
| `AggregateRecommendation_NoPanelists_ReturnsNull`                      | Empty panelist list returns null                     | `null`                 |
| `AggregateRecommendation_NoFeedback_ReturnsNull`                       | Panelists without submitted feedback return null     | `null`                 |
| `AggregateRecommendation_MajorityStrongHire_ReturnsStrongHire`         | 2/3 StrongHire → StrongHire (majority wins)          | `"StrongHire"`         |
| `AggregateRecommendation_Tie_ReturnsHighestVoted`                      | Tie-breaking selects first in count order            | Deterministic result   |
| `AggregateRecommendation_MixedWithPendingFeedback_OnlyCountsSubmitted` | Ignores panelists who haven't submitted feedback yet | Only submitted counted |

**Why:** Recommendation aggregation drives the hire/no-hire decision presented to hiring managers. The majority vote algorithm must handle edge cases correctly — empty panels, ties, and partially-submitted feedback — to prevent incorrect hiring recommendations.

---

## `CandidateShortlistedHRWorkflowsHandlerTests` (2 tests)

Tests `CandidateShortlistedHRWorkflowsHandler` — the domain event handler that automatically creates placeholder final interviews when candidates are shortlisted.

| Test                                                | What It Verifies                                       | Expected Outcome                        |
| --------------------------------------------------- | ------------------------------------------------------ | --------------------------------------- |
| `Handle_NewApplication_CreatesPlaceholderInterview` | Creates FinalInterview with default Video type, 60 min | Interview created with Scheduled status |
| `Handle_AlreadyExists_SkipsCreation`                | Idempotency — doesn't create duplicate interviews      | No repository add, no save              |

**Why:** The event handler bridges the Matching and HR Workflows modules. The idempotency check ensures that reprocessed or duplicate events don't create conflicting interview records.
