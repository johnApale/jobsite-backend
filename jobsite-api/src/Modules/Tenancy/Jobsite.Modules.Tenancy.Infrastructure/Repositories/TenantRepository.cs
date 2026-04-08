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

    public async Task<(List<Tenant> Items, bool HasMore)> GetListAsync(
        string? status, string? search, string? cursor, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Tenant> query = _db.Tenants.AsNoTracking().Include(t => t.Branding);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Subdomain.Contains(search));

        if (!string.IsNullOrWhiteSpace(cursor) && Guid.TryParse(cursor, out Guid cursorId))
            query = query.Where(t => t.Id.CompareTo(cursorId) > 0);

        query = query.OrderBy(t => t.Id);

        List<Tenant> items = await query.Take(pageSize + 1).ToListAsync(ct);
        bool hasMore = items.Count > pageSize;

        if (hasMore)
            items = items.Take(pageSize).ToList();

        return (items, hasMore);
    }

    public async Task<Tenant?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Tenants
            .Include(t => t.Branding)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public void Add(Tenant tenant)
    {
        _db.Tenants.Add(tenant);
    }
}
