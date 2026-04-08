using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.CrossModule;

/// <summary>
/// Provides aggregate screening statistics to the Admin module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class ScreeningStatsReader : IScreeningStatsReader
{
    private readonly ScreeningDbContext _db;

    public ScreeningStatsReader(ScreeningDbContext db) => _db = db;

    public async Task<ScreeningStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
    {
        int totalScreenings = await _db.ScreeningResults.AsNoTracking().CountAsync(ct);
        int completedScreenings = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Status == ScreeningStatus.Completed, ct);
        int pendingScreenings = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Status == ScreeningStatus.Pending || r.Status == ScreeningStatus.InProgress, ct);
        int failedScreenings = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Status == ScreeningStatus.Failed, ct);

        decimal? averageScore = await _db.ScreeningResults.AsNoTracking()
            .Where(r => r.Status == ScreeningStatus.Completed && r.OverallScore != null)
            .AverageAsync(r => (decimal?)r.OverallScore, ct);

        int autoAdvancedCount = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Outcome == ScreeningOutcome.AutoAdvanced, ct);
        int autoRejectedCount = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Outcome == ScreeningOutcome.AutoRejected, ct);
        int manualReviewCount = await _db.ScreeningResults.AsNoTracking()
            .CountAsync(r => r.Outcome == ScreeningOutcome.ManualReview, ct);

        return new ScreeningStatsSnapshot
        {
            TotalScreenings = totalScreenings,
            CompletedScreenings = completedScreenings,
            PendingScreenings = pendingScreenings,
            FailedScreenings = failedScreenings,
            AverageScore = averageScore.HasValue ? Math.Round(averageScore.Value, 2) : null,
            AutoAdvancedCount = autoAdvancedCount,
            AutoRejectedCount = autoRejectedCount,
            ManualReviewCount = manualReviewCount
        };
    }
}
