namespace Jobsite.Modules.HRWorkflows.Application.DTOs;

// ── Requests ─────────────────────────────────────────────────────────────

public sealed class ScheduleInterviewRequest
{
    public required Guid ApplicationId { get; init; }
    public required string InterviewType { get; init; }
    public required DateTime ScheduledAt { get; init; }
    public int DurationMinutes { get; init; } = 60;
    public string? Location { get; init; }
    public required List<Guid> PanelistUserIds { get; init; }
}

public sealed class UpdateInterviewRequest
{
    public string? InterviewType { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public int? DurationMinutes { get; init; }
    public string? Location { get; init; }
}

public sealed class SubmitFeedbackRequest
{
    public required decimal Rating { get; init; }
    public required string Recommendation { get; init; }
    public string? Strengths { get; init; }
    public string? Concerns { get; init; }
    public string? Notes { get; init; }
}

public sealed class RecordDecisionRequest
{
    public required string OverallRecommendation { get; init; }
    public string? DecisionNotes { get; init; }
}

public sealed class CancelInterviewRequest
{
    public required string Reason { get; init; }
}

// ── Responses ────────────────────────────────────────────────────────────

public sealed class PanelistResponse
{
    public required Guid Id { get; init; }
    public required Guid InterviewerId { get; init; }
    public decimal? Rating { get; init; }
    public string? Recommendation { get; init; }
    public string? Strengths { get; init; }
    public string? Concerns { get; init; }
    public string? Notes { get; init; }
    public DateTime? FeedbackSubmittedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class FinalInterviewResponse
{
    public required Guid ApplicationId { get; init; }
    public required string Status { get; init; }
    public required string InterviewType { get; init; }
    public required DateTime ScheduledAt { get; init; }
    public required int DurationMinutes { get; init; }
    public string? Location { get; init; }
    public required Guid ScheduledBy { get; init; }
    public string? OverallRecommendation { get; init; }
    public string? DecisionNotes { get; init; }
    public Guid? DecidedBy { get; init; }
    public DateTime? DecidedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public string? AggregatedRecommendation { get; init; }
    public required List<PanelistResponse> Panelists { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class InterviewListResponse
{
    public required List<FinalInterviewResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed class InterviewQueryParameters
{
    public string? Status { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}
