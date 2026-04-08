namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads aggregate screening statistics from the Screening module.
/// Defined in SharedKernel; implemented by Screening.Infrastructure.
/// Consumed by the Admin module for the dashboard endpoint.
/// </summary>
public interface IScreeningStatsReader
{
    /// <summary>Returns aggregate screening result counts for the current tenant.</summary>
    Task<ScreeningStatsSnapshot> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>Projection of screening statistics needed by the Admin dashboard.</summary>
public sealed class ScreeningStatsSnapshot
{
    public required int TotalScreenings { get; init; }
    public required int CompletedScreenings { get; init; }
    public required int PendingScreenings { get; init; }
    public required int FailedScreenings { get; init; }
    public required decimal? AverageScore { get; init; }
    public required int AutoAdvancedCount { get; init; }
    public required int AutoRejectedCount { get; init; }
    public required int ManualReviewCount { get; init; }
}
