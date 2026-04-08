using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Application.Services;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Admin.Infrastructure.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TenantSettingsReader = Jobsite.Modules.Admin.Infrastructure.Persistence.TenantSettingsReader;

namespace Jobsite.Modules.Admin.Infrastructure;

/// <summary>
/// DI registration for the Admin module.
/// Called from <c>ModuleServiceCollectionExtensions.AddJobsiteModules()</c>.
/// </summary>
public static class AdminModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAdminModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory for per-tenant database resolution
        services.AddScoped<ITenantDbContextFactory<AdminDbContext>, TenantAdminDbContextFactory>();
        services.AddScoped<AdminDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<AdminDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<ICompanySettingsRepository, CompanySettingsRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // Unit of Work (scoped to Admin tenant DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, AdminUnitOfWork>("admin");

        // Services
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Cross-module settings reader (consumed by other modules via SharedKernel interface)
        services.AddScoped<ITenantSettingsReader, TenantSettingsReader>();

        return services;
    }
}
