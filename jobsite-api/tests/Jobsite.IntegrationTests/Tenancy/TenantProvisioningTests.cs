using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Provisioning;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;

namespace Jobsite.IntegrationTests.Tenancy;

/// <summary>
/// Integration tests for tenant database provisioning against a real PostgreSQL container.
/// Validates CREATE DATABASE, connection string assignment, status transitions,
/// and tenant database isolation.
/// </summary>
[Collection("Catalog")]
public sealed class TenantProvisioningTests : IAsyncLifetime
{
    private readonly CatalogIntegrationFixture _fixture;

    public TenantProvisioningTests(CatalogIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();

    public async Task DisposeAsync()
    {
        // Clean up any tenant databases created during tests
        await CleanupTenantDatabasesAsync();
    }

    [Fact]
    public async Task ProvisionAsync_ValidTenant_CreatesDatabaseAndSetsActive()
    {
        // Arrange
        Tenant tenant = IntegrationTestData.CreateTenant(
            name: "Provision Test Corp",
            subdomain: "provtest",
            status: TenantStatus.Provisioning);
        tenant.ConnectionString = string.Empty;

        _fixture.DbContext.Tenants.Add(tenant);
        await _fixture.DbContext.SaveChangesAsync();

        TenantProvisioner provisioner = CreateProvisioner();

        // Act
        await provisioner.ProvisionAsync(tenant.Id, CancellationToken.None);

        // Assert — reload tenant from DB
        Tenant? updated = await _fixture.DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenant.Id);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(TenantStatus.Active);
        updated.ConnectionString.Should().Contain("djobsite_tenant_provtest");
        updated.ProvisionedAt.Should().NotBeNull();
        updated.ProvisionedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Verify the database actually exists
        bool dbExists = await DatabaseExistsAsync("djobsite_tenant_provtest");
        dbExists.Should().BeTrue("tenant database should have been created");
    }

    [Fact]
    public async Task ProvisionAsync_TwoTenants_GetDistinctDatabases()
    {
        // Arrange
        Tenant tenantA = IntegrationTestData.CreateTenant(
            name: "Tenant A Corp",
            subdomain: "isolatea",
            status: TenantStatus.Provisioning);
        tenantA.ConnectionString = string.Empty;

        Tenant tenantB = IntegrationTestData.CreateTenant(
            name: "Tenant B Corp",
            subdomain: "isolateb",
            status: TenantStatus.Provisioning);
        tenantB.ConnectionString = string.Empty;

        _fixture.DbContext.Tenants.Add(tenantA);
        _fixture.DbContext.Tenants.Add(tenantB);
        await _fixture.DbContext.SaveChangesAsync();

        TenantProvisioner provisioner = CreateProvisioner();

        // Act
        await provisioner.ProvisionAsync(tenantA.Id, CancellationToken.None);
        await provisioner.ProvisionAsync(tenantB.Id, CancellationToken.None);

        // Assert — reload both tenants
        Tenant? updatedA = await _fixture.DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantA.Id);
        Tenant? updatedB = await _fixture.DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantB.Id);

        updatedA.Should().NotBeNull();
        updatedB.Should().NotBeNull();

        updatedA!.Status.Should().Be(TenantStatus.Active);
        updatedB!.Status.Should().Be(TenantStatus.Active);

        // Connection strings should point to different databases
        updatedA.ConnectionString.Should().Contain("djobsite_tenant_isolatea");
        updatedB.ConnectionString.Should().Contain("djobsite_tenant_isolateb");
        updatedA.ConnectionString.Should().NotBe(updatedB.ConnectionString,
            "each tenant must have its own database connection string");

        // Both databases should exist
        bool dbAExists = await DatabaseExistsAsync("djobsite_tenant_isolatea");
        bool dbBExists = await DatabaseExistsAsync("djobsite_tenant_isolateb");
        dbAExists.Should().BeTrue();
        dbBExists.Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionAsync_NonExistentTenant_ThrowsTenantNotFound()
    {
        // Arrange
        TenantProvisioner provisioner = CreateProvisioner();

        // Act
        Func<Task> act = async () =>
            await provisioner.ProvisionAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    private TenantProvisioner CreateProvisioner()
    {
        Dictionary<string, string?> configData = new()
        {
            ["ConnectionStrings:CatalogDb"] = _fixture.ConnectionString
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ILogger<TenantProvisioner> logger = Substitute.For<ILogger<TenantProvisioner>>();
        IDomainEventDispatcher dispatcher = Substitute.For<IDomainEventDispatcher>();

        return new TenantProvisioner(_fixture.DbContext, configuration, dispatcher, logger);
    }

    private async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        command.Parameters.AddWithValue("name", databaseName);

        object? result = await command.ExecuteScalarAsync();
        return result is not null;
    }

    private async Task CleanupTenantDatabasesAsync()
    {
        string[] tenantDbs = ["djobsite_tenant_provtest", "djobsite_tenant_isolatea", "djobsite_tenant_isolateb"];

        await using NpgsqlConnection connection = new(_fixture.ConnectionString);
        await connection.OpenAsync();

        foreach (string db in tenantDbs)
        {
            try
            {
                await using NpgsqlCommand terminateCmd = connection.CreateCommand();
                terminateCmd.CommandText = $"""
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = '{db}' AND pid <> pg_backend_pid()
                    """;
                await terminateCmd.ExecuteNonQueryAsync();

                await using NpgsqlCommand dropCmd = connection.CreateCommand();
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{db}\"";
                await dropCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
