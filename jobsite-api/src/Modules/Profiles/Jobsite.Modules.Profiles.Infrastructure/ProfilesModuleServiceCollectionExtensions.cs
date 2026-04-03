using FluentValidation;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Application.Services;
using Jobsite.Modules.Profiles.Infrastructure.Parsing;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;
using Jobsite.Modules.Profiles.Infrastructure.Services;
using Jobsite.Modules.Profiles.Infrastructure.Storage;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Profiles.Infrastructure;

/// <summary>
/// DI registration for the Profiles module.
/// Called from <c>ModuleServiceCollectionExtensions.AddJobsiteModules()</c>.
/// </summary>
public static class ProfilesModuleServiceCollectionExtensions
{
    public static IServiceCollection AddProfilesModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory for per-tenant database resolution
        services.AddScoped<ITenantDbContextFactory<ProfilesDbContext>, TenantProfilesDbContextFactory>();
        services.AddScoped<ProfilesDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<ProfilesDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<IApplicantProfileRepository, ApplicantProfileRepository>();
        services.AddScoped<IResumeRepository, ResumeRepository>();

        // Services
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IResumeService, ResumeService>();

        // File storage
        services.AddScoped<IFileStorage, LocalFileStorage>();

        // Resume parser
        services.AddScoped<IResumeParser, BasicResumeParser>();

        // Unit of Work (scoped to Profiles tenant DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, ProfilesUnitOfWork>("profiles");

        // Validators
        services.AddValidatorsFromAssemblyContaining<Application.Validators.CreateProfileRequestValidator>();

        // Background services
        services.AddHostedService<ResumeParseRecoveryService>();

        return services;
    }
}
