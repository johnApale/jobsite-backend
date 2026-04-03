using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Admin.Infrastructure;

/// <summary>
/// Unit-of-work implementation for the Admin tenant DB.
/// </summary>
public sealed class AdminUnitOfWork : IUnitOfWork
{
    private readonly AdminDbContext _db;

    public AdminUnitOfWork(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
