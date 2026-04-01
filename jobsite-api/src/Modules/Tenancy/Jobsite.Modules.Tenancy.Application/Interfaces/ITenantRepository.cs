using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.Modules.Tenancy.Application.Interfaces;

/// <summary>
/// Repository for tenant lookups against the shared Catalog DB.
/// </summary>
public interface ITenantRepository
{
    /// <summary>Resolve a tenant (with branding) by subdomain. Used by middleware on every request.</summary>
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default);

    /// <summary>Get a tenant by ID.</summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Check if a subdomain is already taken.</summary>
    Task<bool> SubdomainExistsAsync(string subdomain, CancellationToken ct = default);

    /// <summary>Check if a company name is already taken.</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);

    /// <summary>Persist a new tenant.</summary>
    void Add(Tenant tenant);
}
