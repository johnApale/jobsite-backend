using Jobsite.Modules.Matching.Application.DTOs;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>Computes weighted composite scores from screening + assessment.</summary>
public interface IScoreAggregationService
{
    /// <summary>
    /// Computes a composite score using tenant-configured weights.
    /// When <paramref name="assessmentScore"/> is null, uses screening score only.
    /// </summary>
    Task<(decimal CompositeScore, string MatchStrength)> ComputeCompositeScoreAsync(
        decimal screeningScore,
        decimal? assessmentScore,
        CancellationToken ct = default);
}
