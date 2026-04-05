namespace Jobsite.Modules.Matching.Application.DTOs;

/// <summary>Full candidate match response.</summary>
public sealed class CandidateMatchResponse
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required decimal ScreeningScore { get; init; }
    public decimal? AssessmentScore { get; init; }
    public required decimal CompositeScore { get; init; }
    public required string MatchStrength { get; init; }
    public int? Rank { get; init; }
    public required DateTime ScreeningCompletedAt { get; init; }
    public DateTime? AssessmentCompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Paginated list of candidate matches.</summary>
public sealed class CandidateMatchListResponse
{
    public required List<CandidateMatchResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

/// <summary>Query parameters for listing candidate matches.</summary>
public sealed class CandidateMatchQueryParameters
{
    public Guid? JobPostingId { get; init; }
    public string? MatchStrength { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}
