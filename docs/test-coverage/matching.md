# Matching Module Test Coverage

← [Test Coverage](README.md)

> Tests for candidate matching — composite score computation, shortlist generation, event handler integration, and constant validation.

---

## `MatchingConstantsTests` (5 theories)

Tests `IsValid()` methods for all 4 Matching module constant classes and the `FromScore` mapping via `[Theory]` with `[InlineData]`. Values must match PostgreSQL CHECK constraints exactly.

| Constant Class             | Tests | What It Verifies                                                                                           |
| -------------------------- | ----- | ---------------------------------------------------------------------------------------------------------- | --- |
| `MatchStrength`            | 1     | Strong/Good/Moderate/Weak valid; FromScore boundary mapping (80+→Strong, 60+→Good, 40+→Moderate, <40→Weak) |
| `ShortlistStatus`          | 1     | Draft/Finalized valid; lowercase and unknown invalid                                                       |
| `ShortlistCandidateSource` | 1     | Algorithm/Manual valid; lowercase and unknown invalid                                                      |
| `ShortlistCandidateStatus` | 1     | Pending/Approved/Rejected valid; lowercase and unknown invalid                                             |
| `MatchStrength.FromScore`  | 1     | Score boundary mapping: 95→Strong, 80→Strong, 60→Good, 40→Moderate, 25→Weak                                |     |

**Why:** PascalCase enum values must exactly match PostgreSQL CHECK constraints. The `FromScore` boundary tests verify the thresholds that determine candidate ranking labels.

---

## `ScoreAggregationServiceTests` (8 tests)

Tests the `ScoreAggregationService` that computes weighted composite scores from screening and optional assessment scores. Uses configurable per-tenant weights from `MatchingSettings`.

| Test                                                                      | What It Verifies                                           | Expected Outcome               |
| ------------------------------------------------------------------------- | ---------------------------------------------------------- | ------------------------------ |
| `ComputeCompositeScore_NoAssessment_ReturnsScreeningScoreAsComposite`     | Screening-only candidate uses screening score as composite | Composite = 85, Strong         |
| `ComputeCompositeScore_NoAssessment_WeakScore_ReturnsWeak`                | Low screening-only score classified correctly              | Composite = 25, Weak           |
| `ComputeCompositeScore_BothScores_DefaultWeights_ComputesWeightedAverage` | Default 60/40 weights: `80*0.6 + 90*0.4 = 84`              | Composite = 84                 |
| `ComputeCompositeScore_BothScores_EqualWeights_ComputesSimpleAverage`     | 50/50 weights: `(70+50)/2 = 60`                            | Composite = 60                 |
| `ComputeCompositeScore_BothScores_CustomWeights_ComputesCorrectly`        | Custom 80/20 weights: `90*0.8 + 40*0.2 = 80`               | Composite = 80                 |
| `ComputeCompositeScore_ZeroWeights_FallsBackToScreeningScore`             | Zero weights fallback → screening score used directly      | Composite = screening score    |
| `ComputeCompositeScore_NullSettings_UsesDefaults`                         | Null tenant settings → default 60/40 weights applied       | Composite matches default calc |
| `ComputeCompositeScore_RoundsToTwoDecimalPlaces`                          | `77*0.6 + 83*0.4 = 79.40` — verifies 2-decimal rounding    | Composite = 79.40              |

**Why:** Composite score math drives all candidate ranking. Incorrect weights, rounding errors, or null-handling bugs directly affect who appears at the top of shortlists. The boundary and fallback tests ensure the formula never produces incorrect results even when tenant settings are missing or malformed.

---

## `MatchingServiceTests` (5 tests)

Tests `MatchingService` — read operations for candidate matches with repository delegation, sorting, and filtering. Uses NSubstitute to mock repositories.

| Test                                                              | What It Verifies                                          | Expected Outcome             |
| ----------------------------------------------------------------- | --------------------------------------------------------- | ---------------------------- |
| `GetMatchAsync_ExistingMatch_ReturnsResponse`                     | Successful lookup maps entity to response DTO             | Response has matching fields |
| `GetMatchAsync_NonexistentMatch_ThrowsCandidateMatchNotFound`     | Missing match throws `CANDIDATE_MATCH_NOT_FOUND`          | Throws `AppError` (404)      |
| `ListMatchesAsync_WithJobPostingId_ReturnsMatchesSortedByScore`   | Matches sorted by composite score descending (90, 75, 60) | Correct sort order           |
| `ListMatchesAsync_WithMatchStrengthFilter_ReturnsFilteredResults` | Filter by MatchStrength returns only matching records     | Only Strong matches returned |
| `ListMatchesAsync_NoJobPostingId_ThrowsValidation`                | Missing required JobPostingId throws validation error     | Throws `AppError` (400)      |

**Why:** `MatchingService` serves the read endpoints that recruiters use to browse candidate rankings. The sort-by-score test ensures the most qualified candidates appear first. The filter test verifies MatchStrength-based filtering for tiered candidate review workflows.

---

## `ShortlistServiceTests` (18 tests)

Tests `ShortlistService` — the core service for generating, managing, and finalizing shortlists. Covers the complete shortlist lifecycle including candidate addition/removal, approval/rejection, and finalization with domain event dispatch.

| Test                                                                             | What It Verifies                                                                                                                          | Expected Outcome                          |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `GenerateShortlist_WithCandidates_CreatesShortlistWithTopN`                      | Selects top-N by composite score, creates Draft shortlist with ranked candidates                                                          | Draft with 2 candidates, ranks 1-2        |
| `GenerateShortlist_NoCandidates_CreatesEmptyShortlist`                           | No candidates → empty shortlist still created                                                                                             | TotalCandidates = 0                       |
| `GetShortlist_ExistingShortlist_ReturnsResponse`                                 | Successful lookup returns response DTO                                                                                                    | Response has matching fields              |
| `GetShortlist_NonexistentShortlist_ThrowsNotFound`                               | Missing shortlist throws `SHORTLIST_NOT_FOUND`                                                                                            | Throws `AppError` (404)                   |
| `AddCandidate_DraftShortlist_AddsCandidateWithManualSource`                      | Manual addition sets source = Manual                                                                                                      | Candidate added with Manual source        |
| `AddCandidate_FinalizedShortlist_ThrowsConflict`                                 | Cannot add to finalized shortlist                                                                                                         | Throws `AppError` (409)                   |
| `AddCandidate_DuplicateCandidate_ThrowsConflict`                                 | Duplicate ApplicationId on same shortlist rejected                                                                                        | Throws `AppError` (409)                   |
| `RemoveCandidate_ExistingCandidate_SoftRemoves`                                  | Soft-delete sets `RemovedAt` timestamp                                                                                                    | `RemovedAt` is not null                   |
| `RemoveCandidate_FinalizedShortlist_ThrowsConflict`                              | Cannot remove from finalized shortlist                                                                                                    | Throws `AppError` (409)                   |
| `RemoveCandidate_NotFoundOnShortlist_ThrowsNotFound`                             | Missing candidate on shortlist throws 404                                                                                                 | Throws `AppError` (404)                   |
| `FinalizeShortlist_DraftWithApprovedCandidates_UpdatesStatusAndDispatchesEvents` | Finalization: only Approved candidates proceed, dispatches CandidateShortlistedEvent per approved candidate, updates application statuses | Status = Finalized, events dispatched     |
| `FinalizeShortlist_NoPendingOrRejectedOnly_ThrowsInvalidRequest`                 | Shortlist with no Approved candidates cannot be finalized                                                                                 | Throws `AppError` (400) `INVALID_REQUEST` |
| `FinalizeShortlist_AlreadyFinalized_ThrowsConflict`                              | Cannot finalize twice                                                                                                                     | Throws `AppError` (409)                   |
| `ApproveCandidate_DraftShortlist_SetsStatusToApproved`                           | Approving a pending candidate sets status to Approved                                                                                     | Status = `Approved`                       |
| `ApproveCandidate_FinalizedShortlist_ThrowsConflict`                             | Cannot approve on finalized shortlist                                                                                                     | Throws `AppError` (409)                   |
| `ApproveCandidate_NonexistentCandidate_ThrowsNotFound`                           | Missing candidate throws 404                                                                                                              | Throws `AppError` (404)                   |
| `RejectCandidate_DraftShortlist_SetsStatusToRejected`                            | Rejecting a pending candidate sets status to Rejected                                                                                     | Status = `Rejected`                       |
| `RejectCandidate_FinalizedShortlist_ThrowsConflict`                              | Cannot reject on finalized shortlist                                                                                                      | Throws `AppError` (409)                   |

**Why:** `ShortlistService` is the central workflow engine for building and locking candidate shortlists. The lifecycle tests (Draft → add/remove → approve/reject → Finalize) ensure correct state transitions and guard against invalid operations on finalized shortlists. The finalization test verifies that only Approved candidates are included, and the critical side effects: status update via `IApplicationStatusUpdater` and domain event dispatch for downstream HR workflows. The approval/rejection tests validate the Pending → Approved/Rejected state machine.

---

## `CvScreeningCompletedMatchingHandlerTests` (9 tests)

Tests the domain event handler that creates a `CandidateMatch` record when CV screening completes and optionally triggers auto-shortlist generation. Consumes `CvScreeningCompletedEvent` from the Screening module.

| Test                                                             | What It Verifies                                                                | Expected Outcome                    |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------- | ----------------------------------- |
| `Handle_PassedScreening_CreatesCandidateMatch`                   | Passed screening → creates match with screening score, composite, MatchStrength | Match created with score 82, Strong |
| `Handle_FailedScreening_DoesNotCreateMatch`                      | Failed screening → no match created                                             | Repository.Add not called           |
| `Handle_MatchAlreadyExists_SkipsCreation`                        | Idempotency: existing match for same ApplicationId → skip                       | Repository.Add not called           |
| `Handle_NoApplicationData_SkipsCreation`                         | Null application data snapshot → skip gracefully                                | Repository.Add not called           |
| `Handle_NoScreeningScore_SkipsCreation`                          | Null screening score snapshot → skip gracefully                                 | Repository.Add not called           |
| `Handle_AutoGenerateEnabled_EnoughCandidates_GeneratesShortlist` | Auto-generate enabled, match count ≥ shortlist size → generates shortlist       | `GenerateShortlistAsync` called     |
| `Handle_AutoGenerateDisabled_DoesNotGenerateShortlist`           | Auto-generate disabled → no shortlist generation                                | `GenerateShortlistAsync` not called |
| `Handle_DraftShortlistAlreadyExists_SkipsAutoGenerate`           | Existing draft shortlist → skip auto-generation                                 | `GenerateShortlistAsync` not called |
| `Handle_NotEnoughCandidates_SkipsAutoGenerate`                   | Match count below shortlist size threshold → skip                               | `GenerateShortlistAsync` not called |

**Why:** This handler is the entry point for the matching pipeline — triggered by the Screening module's `CvScreeningCompletedEvent`. The idempotency test prevents duplicate matches on event replay. The null-guard tests ensure the handler degrades gracefully when cross-module readers return no data. The auto-generate tests verify the tenant-configurable shortlist generation trigger, including threshold checks and deduplication.

---

## `AssessmentCompletedMatchingHandlerTests` (2 tests)

Tests the domain event handler that updates an existing `CandidateMatch` with assessment scores when assessment completes.

| Test                                                       | What It Verifies                                                                             | Expected Outcome        |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ----------------------- |
| `Handle_ExistingMatch_UpdatesAssessmentScoreAndRecomputes` | Updates assessment score, recomputes composite (80*60% + 90*40% = 84), updates MatchStrength | Composite = 84, Strong  |
| `Handle_NoExistingMatch_SkipsUpdate`                       | No existing match → skip silently                                                            | No exception, no update |

**Why:** Assessment completion is the second scoring signal. The recomputation test verifies the weighted composite formula is re-applied with both scores. The skip test ensures resilience when a match was never created (e.g., screening failed).

---

## `MatchingValidatorTests` (4 tests)

Tests FluentValidation validators for request DTOs.

| Test                                                       | What It Verifies                          | Expected Outcome |
| ---------------------------------------------------------- | ----------------------------------------- | ---------------- |
| `GenerateShortlistRequest_ValidJobPostingId_Passes`        | Non-empty GUID passes validation          | IsValid = true   |
| `GenerateShortlistRequest_EmptyJobPostingId_Fails`         | Empty GUID fails with JobPostingId error  | IsValid = false  |
| `AddCandidateToShortlistRequest_ValidApplicationId_Passes` | Non-empty GUID passes validation          | IsValid = true   |
| `AddCandidateToShortlistRequest_EmptyApplicationId_Fails`  | Empty GUID fails with ApplicationId error | IsValid = false  |

**Why:** Request validation is the first line of defense against malformed API calls. Empty GUID checks prevent silent no-op queries.

---

## Summary

| Test Class                                 | Tests  | Coverage Area                                              |
| ------------------------------------------ | ------ | ---------------------------------------------------------- |
| `MatchingConstantsTests`                   | 5      | Constants, CHECK constraint alignment                      |
| `ScoreAggregationServiceTests`             | 8      | Composite score computation                                |
| `MatchingServiceTests`                     | 5      | Match read operations                                      |
| `ShortlistServiceTests`                    | 18     | Shortlist lifecycle (generate/add/remove/approve/finalize) |
| `CvScreeningCompletedMatchingHandlerTests` | 9      | Cross-module event: screening → matching + auto-generation |
| `AssessmentCompletedMatchingHandlerTests`  | 2      | Cross-module event: assessment → matching                  |
| `MatchingValidatorTests`                   | 4      | Request validation                                         |
| **Total**                                  | **51** | **7 test files**                                           |
