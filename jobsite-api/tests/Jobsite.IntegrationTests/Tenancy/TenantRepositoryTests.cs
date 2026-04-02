using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Tenancy;

/// <summary>
/// Integration tests for TenantRepository against a real PostgreSQL container.
/// Validates EF Core configurations, snake_case mapping, CHECK constraints,
/// unique indexes, and query behavior.
/// </summary>
[Collection("Catalog")]
public sealed class TenantRepositoryTests : IAsyncLifetime
{
    private readonly CatalogIntegrationFixture _fixture;
    private readonly TenantRepository _sut;

    public TenantRepositoryTests(CatalogIntegrationFixture fixture)
    {
        _fixture = fixture;
        _sut = new TenantRepository(fixture.DbContext);
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_ValidTenant_PersistsToDatabase()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(name: "Persist Corp", subdomain: "persist");

        // Act
        _sut.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert — re-query to verify persistence
        Tenant? persisted = await _fixture.DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Subdomain == "persist");

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Persist Corp");
        persisted.Id.Should().NotBe(Guid.Empty, "database should assign a UUID");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetBySubdomainAsync_ExistingTenant_ReturnsTenantWithBranding()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(name: "Subdomain Corp", subdomain: "subtest");
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        TenantBranding branding = IntegrationTestData.CreateBranding(tenant.Id);
        _fixture.DbContext.TenantBrandings.Add(branding);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        Tenant? result = await _sut.GetBySubdomainAsync("subtest", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Subdomain Corp");
        result.Branding.Should().NotBeNull();
        result.Branding!.PrimaryColor.Should().Be("#1A73E8");
    }

    [Fact]
    public async Task GetBySubdomainAsync_NonExistent_ReturnsNull()
    {
        // Arrange & Act
        Tenant? result = await _sut.GetBySubdomainAsync("nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTenant_ReturnsTenant()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(name: "ById Corp", subdomain: "byid");
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        Tenant? result = await _sut.GetByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("ById Corp");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange & Act
        Tenant? result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SubdomainExistsAsync_ExistingSubdomain_ReturnsTrue()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(name: "Exists Corp", subdomain: "exists");
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool result = await _sut.SubdomainExistsAsync("exists", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SubdomainExistsAsync_NonExistent_ReturnsFalse()
    {
        // Arrange & Act
        bool result = await _sut.SubdomainExistsAsync("nope", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task NameExistsAsync_ExistingName_ReturnsTrue()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(name: "Unique Name Corp", subdomain: "namecheck");
        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool result = await _sut.NameExistsAsync("Unique Name Corp", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NameExistsAsync_NonExistent_ReturnsFalse()
    {
        // Arrange & Act
        bool result = await _sut.NameExistsAsync("No Such Corp", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Add_DuplicateSubdomain_ThrowsDbUpdateException()
    {
        // Arrange
        Tenant first = IntegrationTestData.CreateTenant(name: "First Corp", subdomain: "dupsub");
        _fixture.DbContext.Tenants.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        Tenant second = IntegrationTestData.CreateTenant(name: "Second Corp", subdomain: "dupsub");
        _fixture.DbContext.Tenants.Add(second);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — unique index on subdomain should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Add_DuplicateName_ThrowsDbUpdateException()
    {
        // Arrange
        Tenant first = IntegrationTestData.CreateTenant(name: "Dupe Name Corp", subdomain: "dn1");
        _fixture.DbContext.Tenants.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        Tenant second = IntegrationTestData.CreateTenant(name: "Dupe Name Corp", subdomain: "dn2");
        _fixture.DbContext.Tenants.Add(second);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — unique index on name should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Add_InvalidStatus_ThrowsDbUpdateException()
    {
        // Arrange — "Deleted" violates the CHECK constraint chk_tenants_status
        Tenant tenant = IntegrationTestData.CreateTenant(
            name: "Bad Status Corp",
            subdomain: "badstatus",
            status: "Deleted");
        _fixture.DbContext.Tenants.Add(tenant);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — CHECK constraint should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }
}
