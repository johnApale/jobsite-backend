using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for resume lookups against the tenant Profiles DB.
/// </summary>
public sealed class ResumeRepository : IResumeRepository
{
    private readonly ProfilesDbContext _db;

    public ResumeRepository(ProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<Resume?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Resumes
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Resume>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Resume?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.Resumes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.IsLatest, ct);
    }

    public async Task MarkPreviousAsNotLatestAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.Resumes
            .Where(r => r.UserId == userId && r.IsLatest)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsLatest, false), ct);
    }

    public void Add(Resume resume)
    {
        _db.Resumes.Add(resume);
    }
}
