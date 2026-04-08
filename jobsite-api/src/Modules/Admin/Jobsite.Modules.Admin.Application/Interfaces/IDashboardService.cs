using Jobsite.Modules.Admin.Application.DTOs;

namespace Jobsite.Modules.Admin.Application.Interfaces;

/// <summary>Application service interface for dashboard statistics.</summary>
public interface IDashboardService
{
    /// <summary>Returns aggregate pipeline statistics across all modules for the current tenant.</summary>
    Task<DashboardStatsResponse> GetStatsAsync(CancellationToken ct = default);
}
