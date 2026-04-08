using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Matching.Infrastructure.CrossModule;

/// <summary>
/// Provides aggregate matching statistics to the Admin module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class MatchingStatsReader : IMatchingStatsReader
{
    private readonly MatchingDbContext _db;

    public MatchingStatsReader(MatchingDbContext db) => _db = db;

    public async Task<MatchingStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
    {
        int totalShortlists = await _db.Shortlists.AsNoTracking().CountAsync(ct);
        int draftShortlists = await _db.Shortlists.AsNoTracking()
            .CountAsync(s => s.Status == ShortlistStatus.Draft, ct);
        int finalizedShortlists = await _db.Shortlists.AsNoTracking()
            .CountAsync(s => s.Status == ShortlistStatus.Finalized, ct);
        int totalCandidateMatches = await _db.CandidateMatches.AsNoTracking().CountAsync(ct);

        return new MatchingStatsSnapshot
        {
            TotalShortlists = totalShortlists,
            DraftShortlists = draftShortlists,
            FinalizedShortlists = finalizedShortlists,
            TotalCandidateMatches = totalCandidateMatches
        };
    }
}
