using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;

/// <summary>
/// Verifies resume ownership by querying the Profiles tenant DB.
/// Registered in SharedKernel's <see cref="IResumeOwnershipVerifier"/> so the
/// Recruitment module can validate resume ownership without a cross-module project reference.
/// </summary>
public sealed class ResumeOwnershipVerifier : IResumeOwnershipVerifier
{
    private readonly ProfilesDbContext _db;

    public ResumeOwnershipVerifier(ProfilesDbContext db) => _db = db;

    public async Task<bool> IsOwnedByUserAsync(Guid resumeId, Guid userId, CancellationToken ct = default)
    {
        return await _db.Resumes
            .AsNoTracking()
            .AnyAsync(r => r.Id == resumeId && r.UserId == userId, ct);
    }
}
