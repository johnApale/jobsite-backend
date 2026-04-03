using FluentValidation;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Jobsite.Modules.Recruitment.Infrastructure;

/// <summary>
/// DI registration for the Recruitment module.
/// Called from <c>ModuleServiceCollectionExtensions.AddJobsiteModules()</c>.
/// </summary>
public static class RecruitmentModuleServiceCollectionExtensions
{
    public static IServiceCollection AddRecruitmentModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory for per-tenant database resolution
        services.AddScoped<ITenantDbContextFactory<RecruitmentDbContext>, TenantRecruitmentDbContextFactory>();
        services.AddScoped<RecruitmentDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<RecruitmentDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IClientCompanyRepository, ClientCompanyRepository>();
        services.AddScoped<ICriteriaRepository, CriteriaRepository>();
        services.AddScoped<IScreeningQuestionRepository, ScreeningQuestionRepository>();

        // Services
        services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IClientCompanyService, ClientCompanyService>();
        services.AddScoped<ICriteriaService, CriteriaService>();
        services.AddScoped<IScreeningQuestionService, ScreeningQuestionService>();

        // AI integration (HTTP clients with resilience policies)
        string aiServiceUrl = configuration["App:AiServiceUrl"] ?? "http://localhost:8000";

        services.AddHttpClient<IAiCriteriaSuggester, AiCriteriaSuggesterClient>(client =>
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

        services.AddHttpClient<IAiQuestionSuggester, AiQuestionSuggesterClient>(client =>
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

        // Unit of Work (scoped to Recruitment tenant DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, RecruitmentUnitOfWork>("recruitment");

        // Validators
        services.AddValidatorsFromAssemblyContaining<Application.Validators.CreateJobPostingRequestValidator>();

        return services;
    }
}
