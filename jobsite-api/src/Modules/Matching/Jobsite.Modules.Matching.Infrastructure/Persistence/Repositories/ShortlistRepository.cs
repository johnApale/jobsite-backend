using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence.Repositories;

public sealed class ShortlistRepository : IShortlistRepository
{
    private readonly MatchingDbContext _db;

    public ShortlistRepository(MatchingDbContext db)
    {
        _db = db;
    }

    public async Task<Shortlist?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Shortlists
            .AsNoTracking()
            .Include(s => s.Candidates)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Shortlist?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Shortlists
            .Include(s => s.Candidates)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<Shortlist?> GetDraftByJobPostingIdAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.Shortlists
            .AsNoTracking()
            .Include(s => s.Candidates)
            .FirstOrDefaultAsync(s =>
                s.JobPostingId == jobPostingId &&
                s.Status == Domain.Constants.ShortlistStatus.Draft, ct);
    }

    public async Task<List<Shortlist>> GetByJobPostingIdAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.Shortlists
            .AsNoTracking()
            .Include(s => s.Candidates)
            .Where(s => s.JobPostingId == jobPostingId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public void Add(Shortlist shortlist)
    {
        _db.Shortlists.Add(shortlist);
    }
}
