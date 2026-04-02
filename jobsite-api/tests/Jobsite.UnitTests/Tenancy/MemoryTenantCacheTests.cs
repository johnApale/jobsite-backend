using FluentAssertions;
using Jobsite.Modules.Tenancy.Infrastructure.Caching;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Jobsite.UnitTests.Tenancy;

public sealed class MemoryTenantCacheTests
{
    private readonly MemoryTenantCache _cache;

    public MemoryTenantCacheTests()
    {
        IMemoryCache memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cache = new MemoryTenantCache(memoryCache);
    }

    [Fact]
    public async Task GetBySubdomainAsync_CacheMiss_ReturnsNull()
    {
        // Act
        Tenant? result = await _cache.GetBySubdomainAsync("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_CacheHit_ReturnsCachedTenant()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(subdomain: "acme");
        await _cache.SetAsync("acme", tenant, CancellationToken.None);

        // Act
        Tenant? result = await _cache.GetBySubdomainAsync("acme", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tenant.Id);
        result.Subdomain.Should().Be("acme");
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedEntry()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(subdomain: "acme");
        await _cache.SetAsync("acme", tenant, CancellationToken.None);

        // Act
        await _cache.InvalidateAsync("acme", CancellationToken.None);

        // Assert
        Tenant? result = await _cache.GetBySubdomainAsync("acme", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_NonexistentKey_DoesNotThrow()
    {
        // Act
        Func<Task> act = () => _cache.InvalidateAsync("nonexistent", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        // Arrange
        Tenant original = TestData.CreateTenant(subdomain: "acme", name: "Original");
        Tenant updated = TestData.CreateTenant(subdomain: "acme", name: "Updated");
        await _cache.SetAsync("acme", original, CancellationToken.None);

        // Act
        await _cache.SetAsync("acme", updated, CancellationToken.None);

        // Assert
        Tenant? result = await _cache.GetBySubdomainAsync("acme", CancellationToken.None);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated");
    }
}
