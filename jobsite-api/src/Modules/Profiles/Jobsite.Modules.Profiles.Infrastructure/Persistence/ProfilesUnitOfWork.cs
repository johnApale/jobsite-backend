using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence;

/// <summary>
/// Unit-of-work implementation for the Profiles tenant DB.
/// </summary>
public sealed class ProfilesUnitOfWork : IUnitOfWork
{
    private readonly ProfilesDbContext _db;

    public ProfilesUnitOfWork(ProfilesDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
