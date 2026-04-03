using FluentValidation;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Application.Services;
using Jobsite.Modules.Profiles.Infrastructure.AiIntegration;
using Jobsite.Modules.Profiles.Infrastructure.Parsing;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;
using Jobsite.Modules.Profiles.Infrastructure.Services;
using Jobsite.Modules.Profiles.Infrastructure.Storage;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

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

        // AI resume parser (HTTP client with resilience policies)
        string aiServiceUrl = configuration["App:AiServiceUrl"] ?? "http://localhost:8000";
        services.AddHttpClient<IAiResumeParser, AiResumeParserClient>(client =>
        {
            client.BaseAddress = new Uri(aiServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        // Unit of Work (scoped to Profiles tenant DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, ProfilesUnitOfWork>("profiles");

        // Validators
        services.AddValidatorsFromAssemblyContaining<Application.Validators.CreateProfileRequestValidator>();

        // Background services
        services.AddHostedService<ResumeParseRecoveryService>();

        return services;
    }
}
