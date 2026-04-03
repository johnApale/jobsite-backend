using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Api.Infrastructure;

/// <summary>
/// Resolves tenant connection strings from the catalog database.
/// Used by MassTransit consumers and background services.
/// </summary>
public sealed class CatalogTenantConnectionResolver : ITenantConnectionResolver
{
    private readonly CatalogDbContext _catalogDb;

    public CatalogTenantConnectionResolver(CatalogDbContext catalogDb)
    {
        _catalogDb = catalogDb;
    }

    public async Task<string> GetConnectionStringAsync(Guid tenantId, CancellationToken ct = default)
    {
        Tenant? tenant = await _catalogDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (tenant is null)
            throw AppErrors.TenantNotFound;

        return tenant.ConnectionString;
    }

    public async Task<List<TenantConnection>> GetAllConnectionsAsync(CancellationToken ct = default)
    {
        return await _catalogDb.Tenants
            .AsNoTracking()
            .Where(t => t.Status == TenantStatus.Active)
            .Select(t => new TenantConnection
            {
                TenantId = t.Id,
                ConnectionString = t.ConnectionString
            })
            .ToListAsync(ct);
    }
}
