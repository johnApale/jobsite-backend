using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence.Repositories;

public sealed class CandidateMatchRepository : ICandidateMatchRepository
{
    private readonly MatchingDbContext _db;

    public CandidateMatchRepository(MatchingDbContext db)
    {
        _db = db;
    }

    public async Task<CandidateMatch?> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.CandidateMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ApplicationId == applicationId, ct);
    }

    public async Task<CandidateMatch?> GetByApplicationIdForUpdateAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.CandidateMatches
            .FirstOrDefaultAsync(m => m.ApplicationId == applicationId, ct);
    }

    public async Task<List<CandidateMatch>> GetByJobPostingIdAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.CandidateMatches
            .AsNoTracking()
            .Where(m => m.JobPostingId == jobPostingId)
            .OrderByDescending(m => m.CompositeScore)
            .ToListAsync(ct);
    }

    public void Add(CandidateMatch match)
    {
        _db.CandidateMatches.Add(match);
    }
}
