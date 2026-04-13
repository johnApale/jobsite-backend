using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Jobsite.Modules.Tenancy.Infrastructure.Provisioning;

/// <summary>
/// Creates a per-tenant PostgreSQL database, runs EF Core migrations,
/// and updates the tenant record to <c>Active</c>.
/// On failure, sets status to <c>ProvisioningFailed</c>.
/// </summary>
public sealed class TenantProvisioner : ITenantProvisioner
{
    private readonly CatalogDbContext _catalogDb;
    private readonly IConfiguration _configuration;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ILogger<TenantProvisioner> _logger;

    public TenantProvisioner(
        CatalogDbContext catalogDb,
        IConfiguration configuration,
        IDomainEventDispatcher dispatcher,
        ILogger<TenantProvisioner> logger)
    {
        _catalogDb = catalogDb;
        _configuration = configuration;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ProvisionAsync(Guid tenantId, CancellationToken ct = default)
    {
        Tenant tenant = await _catalogDb.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw AppErrors.TenantNotFound;

        string databaseName = $"jobsite_tenant_{tenant.Subdomain}";
        string tenantConnectionString = BuildTenantConnectionString(databaseName);

        try
        {
            await CreateDatabaseAsync(databaseName, ct);

            _logger.LogInformation(
                "Database {DatabaseName} created for tenant {TenantId}",
                databaseName, tenantId);

            tenant.ConnectionString = tenantConnectionString;
            tenant.Status = TenantStatus.Active;
            tenant.ProvisionedAt = DateTime.UtcNow;

            await _catalogDb.SaveChangesAsync(ct);

            await _dispatcher.DispatchAsync(new TenantProvisionedEvent
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                OwnerEmail = tenant.OwnerEmail,
                ConnectionString = tenantConnectionString,
                ProvisionedAt = tenant.ProvisionedAt!.Value
            }, ct);

            _logger.LogInformation(
                "Tenant {TenantId} provisioned successfully with database {DatabaseName}",
                tenantId, databaseName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to provision tenant {TenantId} with database {DatabaseName}",
                tenantId, databaseName);

            tenant.Status = TenantStatus.ProvisioningFailed;
            await _catalogDb.SaveChangesAsync(ct);
        }
    }

    private async Task CreateDatabaseAsync(string databaseName, CancellationToken ct)
    {
        string catalogConnectionString = _configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("CatalogDb connection string is required");

        await using NpgsqlConnection connection = new(catalogConnectionString);
        await connection.OpenAsync(ct);

        // Parameterized database names not supported in DDL; sanitize to prevent SQL injection
        string sanitizedName = SanitizeDatabaseName(databaseName);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{sanitizedName}\"";
        await command.ExecuteNonQueryAsync(ct);
    }

    private string BuildTenantConnectionString(string databaseName)
    {
        string catalogConnectionString = _configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("CatalogDb connection string is required");

        NpgsqlConnectionStringBuilder builder = new(catalogConnectionString)
        {
            Database = databaseName
        };

        return builder.ConnectionString;
    }

    /// <summary>
    /// Sanitize database name to prevent SQL injection in DDL statements.
    /// Only allows alphanumeric characters and underscores.
    /// </summary>
    private static string SanitizeDatabaseName(string name)
    {
        foreach (char c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                throw new ArgumentException(
                    $"Invalid character '{c}' in database name '{name}'. Only alphanumeric and underscore allowed.");
            }
        }

        return name;
    }
}
