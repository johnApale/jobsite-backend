namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads screening scores for an application from the Screening module.
/// Defined in SharedKernel; implemented by Screening.Infrastructure.
/// Consumed by the Matching module for composite score computation.
/// </summary>
public interface IScreeningScoreReader
{
    /// <summary>
    /// Returns the screening scores for an application, or <c>null</c> if no screening result exists.
    /// </summary>
    Task<ScreeningScoreSnapshot?> GetScoreAsync(Guid applicationId, CancellationToken ct = default);
}

/// <summary>Projection of screening scores needed by the Matching module.</summary>
public sealed class ScreeningScoreSnapshot
{
    public required decimal OverallScore { get; init; }
    public decimal? AiOverallScore { get; init; }
    public required string MatchStrength { get; init; }
    public required string Status { get; init; }
}
