using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Auth.Infrastructure;

/// <summary>
/// Unit-of-work implementation for the Auth tenant DB.
/// </summary>
public sealed class AuthUnitOfWork : IUnitOfWork
{
    private readonly AuthDbContext _db;

    public AuthUnitOfWork(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
