using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for applicant profile lookups against the tenant Profiles DB.
/// </summary>
public sealed class ApplicantProfileRepository : IApplicantProfileRepository
{
    private readonly ProfilesDbContext _db;

    public ApplicantProfileRepository(ProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<ApplicantProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ApplicantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId, ct);
    }

    public async Task<ApplicantProfile?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ApplicantProfiles
            .FirstOrDefaultAsync(p => p.Id == userId, ct);
    }

    public async Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ApplicantProfiles.AnyAsync(p => p.Id == userId, ct);
    }

    public void Add(ApplicantProfile profile)
    {
        _db.ApplicantProfiles.Add(profile);
    }
}
