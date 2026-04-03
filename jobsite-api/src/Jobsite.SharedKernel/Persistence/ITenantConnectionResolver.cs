namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Resolves tenant database connection strings from tenant IDs.
/// Used by MassTransit consumers and background services that don't have HttpContext.
/// Implemented in the Api layer via the catalog database.
/// </summary>
public interface ITenantConnectionResolver
{
    /// <summary>Look up the connection string for a single tenant.</summary>
    Task<string> GetConnectionStringAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Get all active tenant IDs and their connection strings.</summary>
    Task<List<TenantConnection>> GetAllConnectionsAsync(CancellationToken ct = default);
}

/// <summary>Lightweight tenant ID + connection string pair.</summary>
public sealed class TenantConnection
{
    public required Guid TenantId { get; init; }
    public required string ConnectionString { get; init; }
}
