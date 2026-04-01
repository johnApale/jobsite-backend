using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Tenancy.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for tenant lookups against the Catalog DB.
/// </summary>
public sealed class TenantRepository : ITenantRepository
{
    private readonly CatalogDbContext _db;

    public TenantRepository(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Subdomain == subdomain, ct);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<bool> SubdomainExistsAsync(string subdomain, CancellationToken ct = default)
    {
        return await _db.Tenants.AnyAsync(t => t.Subdomain == subdomain, ct);
    }

    public async Task<bool> NameExistsAsync(string name, CancellationToken ct = default)
    {
        return await _db.Tenants.AnyAsync(t => t.Name == name, ct);
    }

    public void Add(Tenant tenant)
    {
        _db.Tenants.Add(tenant);
    }
}
