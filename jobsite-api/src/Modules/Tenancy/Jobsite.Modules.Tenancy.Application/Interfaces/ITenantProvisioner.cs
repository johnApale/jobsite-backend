namespace Jobsite.Modules.Tenancy.Application.Interfaces;

/// <summary>
/// Creates a per-tenant database, runs migrations, and activates the tenant.
/// On failure, sets tenant status to <c>ProvisioningFailed</c>.
/// </summary>
public interface ITenantProvisioner
{
    /// <summary>
    /// Provision a tenant database: CREATE DATABASE → run migrations → update status to Active.
    /// </summary>
    Task ProvisionAsync(Guid tenantId, CancellationToken ct = default);
}
