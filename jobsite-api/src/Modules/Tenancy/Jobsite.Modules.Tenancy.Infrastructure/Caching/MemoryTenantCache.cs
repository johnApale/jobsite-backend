using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Jobsite.Modules.Tenancy.Infrastructure.Caching;

/// <summary>
/// In-memory tenant cache with 5-minute sliding expiration.
/// Swap to <c>RedisTenantCache</c> by changing the DI registration
/// in <see cref="TenancyModuleServiceCollectionExtensions"/>.
/// </summary>
public sealed class MemoryTenantCache : ITenantCache
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache _cache;

    public MemoryTenantCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        Tenant? tenant = _cache.Get<Tenant>(key);
        return Task.FromResult(tenant);
    }

    public Task SetAsync(string subdomain, Tenant tenant, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        MemoryCacheEntryOptions options = new()
        {
            SlidingExpiration = SlidingExpiration
        };
        _cache.Set(key, tenant, options);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string subdomain, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    private static string CacheKey(string subdomain) => $"tenant:subdomain:{subdomain}";
}
