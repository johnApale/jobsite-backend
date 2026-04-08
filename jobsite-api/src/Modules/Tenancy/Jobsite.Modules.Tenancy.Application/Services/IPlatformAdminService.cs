using Jobsite.Modules.Tenancy.Application.DTOs;

namespace Jobsite.Modules.Tenancy.Application.Services;

/// <summary>Application service interface for platform-wide tenant administration.</summary>
public interface IPlatformAdminService
{
    /// <summary>Returns a paginated list of tenants with optional filters.</summary>
    Task<TenantListResponse> GetTenantsAsync(TenantQueryParameters parameters, CancellationToken ct = default);

    /// <summary>Gets a single tenant by ID.</summary>
    Task<TenantResponse> GetTenantByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Suspends a tenant, preventing access.</summary>
    Task<TenantResponse> SuspendTenantAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reactivates a previously suspended tenant.</summary>
    Task<TenantResponse> ReactivateTenantAsync(Guid id, CancellationToken ct = default);
}
