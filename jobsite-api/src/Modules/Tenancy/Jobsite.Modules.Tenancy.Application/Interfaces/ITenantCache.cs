using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.Modules.Tenancy.Application.Interfaces;

/// <summary>
/// Cache abstraction for tenant resolution.
/// Backed by <c>IMemoryCache</c> initially; swap to Redis via one-line DI change.
/// </summary>
public interface ITenantCache
{
    /// <summary>Retrieve a cached tenant by subdomain. Returns null on cache miss.</summary>
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default);

    /// <summary>Store a tenant in the cache, keyed by subdomain.</summary>
    Task SetAsync(string subdomain, Tenant tenant, CancellationToken ct = default);

    /// <summary>Remove a cached tenant entry. Call when tenant data changes.</summary>
    Task InvalidateAsync(string subdomain, CancellationToken ct = default);
}
