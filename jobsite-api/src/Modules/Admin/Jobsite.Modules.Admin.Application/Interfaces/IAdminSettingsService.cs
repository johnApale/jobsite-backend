using Jobsite.Modules.Admin.Application.DTOs;

namespace Jobsite.Modules.Admin.Application.Interfaces;

/// <summary>Application service for tenant settings management.</summary>
public interface IAdminSettingsService
{
    /// <summary>Get the current tenant settings.</summary>
    Task<CompanySettingsResponse> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Partially update tenant settings (JSON merge patch semantics).</summary>
    Task<CompanySettingsResponse> UpdateSettingsAsync(UpdateCompanySettingsRequest request, CancellationToken ct = default);
}
