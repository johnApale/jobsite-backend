using System.Text.Json;
using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace Jobsite.Modules.Tenancy.Infrastructure.Caching;

/// <summary>
/// Distributed tenant cache backed by <see cref="IDistributedCache"/>.
/// Works with both Redis (production) and in-memory (development) depending on DI registration.
/// Uses 5-minute sliding expiration.
/// </summary>
public sealed class DistributedTenantCache : ITenantCache
{
    private static readonly TimeSpan SlidingExpiration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IDistributedCache _cache;

    public DistributedTenantCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        byte[]? data = await _cache.GetAsync(key, ct);

        if (data is null || data.Length == 0)
            return null;

        return JsonSerializer.Deserialize<Tenant>(data, JsonOptions);
    }

    public async Task SetAsync(string subdomain, Tenant tenant, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(tenant, JsonOptions);

        DistributedCacheEntryOptions options = new()
        {
            SlidingExpiration = SlidingExpiration
        };

        await _cache.SetAsync(key, data, options, ct);
    }

    public async Task InvalidateAsync(string subdomain, CancellationToken ct = default)
    {
        string key = CacheKey(subdomain);
        await _cache.RemoveAsync(key, ct);
    }

    private static string CacheKey(string subdomain) => $"tenant:subdomain:{subdomain}";
}
