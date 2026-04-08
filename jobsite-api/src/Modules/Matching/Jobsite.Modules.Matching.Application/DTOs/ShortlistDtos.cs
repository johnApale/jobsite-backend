namespace Jobsite.Modules.Matching.Application.DTOs;

/// <summary>Shortlist candidate detail.</summary>
public sealed class ShortlistCandidateResponse
{
    public required Guid Id { get; init; }
    public required Guid ApplicationId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required decimal CompositeScore { get; init; }
    public required int Rank { get; init; }
    public required string Source { get; init; }
    public required string Status { get; init; }
    public required DateTime AddedAt { get; init; }
    public DateTime? RemovedAt { get; init; }
}

/// <summary>Full shortlist response with embedded candidates.</summary>
public sealed class ShortlistResponse
{
    public required Guid Id { get; init; }
    public required Guid JobPostingId { get; init; }
    public required string Status { get; init; }
    public required string GeneratedBy { get; init; }
    public required int TotalCandidates { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public Guid? FinalizedBy { get; init; }
    public required List<ShortlistCandidateResponse> Candidates { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Paginated list of shortlists (without embedded candidates).</summary>
public sealed class ShortlistListResponse
{
    public required List<ShortlistSummaryResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

/// <summary>Summary shortlist for list endpoints (no embedded candidates).</summary>
public sealed class ShortlistSummaryResponse
{
    public required Guid Id { get; init; }
    public required Guid JobPostingId { get; init; }
    public required string Status { get; init; }
    public required string GeneratedBy { get; init; }
    public required int TotalCandidates { get; init; }
    public DateTime? FinalizedAt { get; init; }
    public Guid? FinalizedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Query parameters for listing shortlists.</summary>
public sealed class ShortlistQueryParameters
{
    public Guid? JobPostingId { get; init; }
    public string? Status { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}

/// <summary>Request to generate a shortlist for a job posting.</summary>
public sealed class GenerateShortlistRequest
{
    public Guid JobPostingId { get; init; }
}

/// <summary>Request to add a candidate manually to a shortlist.</summary>
public sealed class AddCandidateToShortlistRequest
{
    public Guid ApplicationId { get; init; }
}
