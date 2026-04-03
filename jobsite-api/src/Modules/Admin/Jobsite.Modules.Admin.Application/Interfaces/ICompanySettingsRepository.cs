using Jobsite.Modules.Admin.Domain.Entities;

namespace Jobsite.Modules.Admin.Application.Interfaces;

/// <summary>Repository for the singleton <c>admin.company_settings</c> row.</summary>
public interface ICompanySettingsRepository
{
    /// <summary>Get the singleton settings row (read-only).</summary>
    Task<CompanySettings?> GetAsync(CancellationToken ct = default);

    /// <summary>Get the singleton settings row for update (tracked).</summary>
    Task<CompanySettings?> GetForUpdateAsync(CancellationToken ct = default);

    /// <summary>Add a new settings row (used during tenant provisioning).</summary>
    void Add(CompanySettings settings);
}
