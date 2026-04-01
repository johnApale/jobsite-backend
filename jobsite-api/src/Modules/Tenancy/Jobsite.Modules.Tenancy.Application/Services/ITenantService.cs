using Jobsite.Modules.Tenancy.Application.DTOs;

namespace Jobsite.Modules.Tenancy.Application.Services;

/// <summary>Application service interface for tenant operations.</summary>
public interface ITenantService
{
    Task<TenantResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantResponse> RegisterAsync(RegisterTenantRequest request, CancellationToken ct = default);
}
