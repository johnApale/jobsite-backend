using Jobsite.SharedKernel.Persistence;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;

namespace Jobsite.Modules.Tenancy.Infrastructure;

/// <summary>
/// Unit-of-work implementation for the Catalog DB.
/// </summary>
public sealed class CatalogUnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _db;

    public CatalogUnitOfWork(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
