using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.CrossModule;

/// <summary>
/// Provides screening scores to the Matching module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class ScreeningScoreReader : IScreeningScoreReader
{
    private readonly ScreeningDbContext _db;

    public ScreeningScoreReader(ScreeningDbContext db) => _db = db;

    public async Task<ScreeningScoreSnapshot?> GetScoreAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.ScreeningResults
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId)
            .Select(r => new ScreeningScoreSnapshot
            {
                OverallScore = r.OverallScore ?? 0,
                AiOverallScore = r.AiOverallScore,
                MatchStrength = r.MatchStrength ?? "Weak",
                Status = r.Status
            })
            .FirstOrDefaultAsync(ct);
    }
}
