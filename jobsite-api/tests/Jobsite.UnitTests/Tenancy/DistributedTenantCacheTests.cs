using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace Jobsite.UnitTests.Tenancy;

/// <summary>
/// Tests for DistributedTenantCache — verifies cache get/set/invalidate
/// operations using IDistributedCache.
/// </summary>
public sealed class DistributedTenantCacheTests
{
    private readonly IDistributedCache _distributedCache;
    private readonly DistributedTenantCache _sut;

    public DistributedTenantCacheTests()
    {
        _distributedCache = Substitute.For<IDistributedCache>();
        _sut = new DistributedTenantCache(_distributedCache);
    }

    [Fact]
    public async Task GetBySubdomainAsync_CacheHit_ReturnsTenant()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(subdomain: "acme");
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(tenant, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        _distributedCache.GetAsync("tenant:subdomain:acme", Arg.Any<CancellationToken>())
            .Returns(serialized);

        // Act
        Tenant? result = await _sut.GetBySubdomainAsync("acme", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Subdomain.Should().Be("acme");
    }

    [Fact]
    public async Task GetBySubdomainAsync_CacheMiss_ReturnsNull()
    {
        // Arrange
        _distributedCache.GetAsync("tenant:subdomain:unknown", Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);

        // Act
        Tenant? result = await _sut.GetBySubdomainAsync("unknown", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBySubdomainAsync_EmptyBytes_ReturnsNull()
    {
        // Arrange
        _distributedCache.GetAsync("tenant:subdomain:empty", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<byte>());

        // Act
        Tenant? result = await _sut.GetBySubdomainAsync("empty", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ValidTenant_StoresInCache()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(subdomain: "acme");

        // Act
        await _sut.SetAsync("acme", tenant, CancellationToken.None);

        // Assert
        await _distributedCache.Received(1).SetAsync(
            "tenant:subdomain:acme",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o => o.SlidingExpiration == TimeSpan.FromMinutes(5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateAsync_ExistingKey_RemovesFromCache()
    {
        // Arrange & Act
        await _sut.InvalidateAsync("acme", CancellationToken.None);

        // Assert
        await _distributedCache.Received(1).RemoveAsync(
            "tenant:subdomain:acme",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsCorrectly()
    {
        // Arrange — use a real in-memory distributed cache for round-trip verification
        Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache realCache = new(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
        DistributedTenantCache realSut = new(realCache);
        Tenant tenant = TestData.CreateTenant(subdomain: "roundtrip");

        // Act
        await realSut.SetAsync("roundtrip", tenant, CancellationToken.None);
        Tenant? result = await realSut.GetBySubdomainAsync("roundtrip", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Subdomain.Should().Be("roundtrip");
        result.Id.Should().Be(tenant.Id);
    }
}
