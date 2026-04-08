using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Infrastructure.Caching;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.Modules.Tenancy.Infrastructure.Provisioning;
using Jobsite.Modules.Tenancy.Infrastructure.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Tenancy.Infrastructure;

/// <summary>
/// DI registration for the Tenancy module.
/// Called from <c>ModuleServiceCollectionExtensions.AddJobsiteModules()</c>.
/// </summary>
public static class TenancyModuleServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("CatalogDb connection string is required");

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .UseSnakeCaseNamingConvention());

        // Repositories
        services.AddScoped<ITenantRepository, TenantRepository>();

        // Unit of Work (scoped to catalog DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, CatalogUnitOfWork>("catalog");

        // Caching (uses IDistributedCache — Redis or memory depending on host DI)
        services.AddSingleton<ITenantCache, DistributedTenantCache>();

        // Provisioning
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();

        // Services
        services.AddScoped<ITenantService, TenantService>();

        return services;
    }
}
