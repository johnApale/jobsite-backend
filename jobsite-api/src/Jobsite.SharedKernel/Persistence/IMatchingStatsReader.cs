namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads aggregate matching statistics from the Matching module.
/// Defined in SharedKernel; implemented by Matching.Infrastructure.
/// Consumed by the Admin module for the dashboard endpoint.
/// </summary>
public interface IMatchingStatsReader
{
    /// <summary>Returns aggregate shortlist and candidate match counts for the current tenant.</summary>
    Task<MatchingStatsSnapshot> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>Projection of matching statistics needed by the Admin dashboard.</summary>
public sealed class MatchingStatsSnapshot
{
    public required int TotalShortlists { get; init; }
    public required int DraftShortlists { get; init; }
    public required int FinalizedShortlists { get; init; }
    public required int TotalCandidateMatches { get; init; }
}
