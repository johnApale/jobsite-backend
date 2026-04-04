namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Full screening result response with all breakdowns.</summary>
public sealed class ScreeningResultResponse
{
    public required Guid ApplicationId { get; init; }
    public required string Status { get; init; }
    public decimal? OverallScore { get; init; }
    public string? MatchStrength { get; init; }
    public string? Outcome { get; init; }
    public string? CriteriaScoreBreakdown { get; init; }
    public string? AiCriteriaScoreBreakdown { get; init; }
    public decimal? AiOverallScore { get; init; }
    public string? QuestionScoreBreakdown { get; init; }
    public decimal? AssessmentScore { get; init; }
    public string? CandidateFeedback { get; init; }
    public decimal AutoAdvanceThreshold { get; init; }
    public decimal AutoRejectThreshold { get; init; }
    public Guid? ReviewedBy { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewNotes { get; init; }
    public string? FailureReason { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Paginated list of screening results.</summary>
public sealed class ScreeningResultListResponse
{
    public required List<ScreeningResultResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

/// <summary>Query parameters for listing screening results.</summary>
public sealed class ScreeningResultQueryParameters
{
    public Guid? JobPostingId { get; init; }
    public string? Status { get; init; }
    public string? Outcome { get; init; }
    public string? MatchStrength { get; init; }
    public decimal? MinScore { get; init; }
    public decimal? MaxScore { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}
