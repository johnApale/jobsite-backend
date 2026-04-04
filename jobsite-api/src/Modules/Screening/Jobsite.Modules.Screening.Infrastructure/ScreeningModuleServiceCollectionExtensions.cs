using FluentValidation;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Application.Validators;
using Jobsite.Modules.Screening.Infrastructure.AiIntegration;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.Modules.Screening.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Screening.Infrastructure;

public static class ScreeningModuleServiceCollectionExtensions
{
    public static IServiceCollection AddScreeningModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory + scoped resolution
        services.AddScoped<ITenantDbContextFactory<ScreeningDbContext>, TenantScreeningDbContextFactory>();
        services.AddScoped<ScreeningDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<ScreeningDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<IScreeningResultRepository, ScreeningResultRepository>();
        services.AddScoped<IScreeningQuestionResponseRepository, ScreeningQuestionResponseRepository>();

        // Application services
        services.AddScoped<IScreeningService, ScreeningService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<QuestionScoringService>();
        services.AddScoped<CandidateFeedbackService>();
        services.AddScoped<IDeterministicScoringEngine, DeterministicScoringEngine>();

        // AI integration (HttpClient + resilience)
        string aiServiceUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";

        services.AddHttpClient<IAiScoringClient, AiScoringClient>(client =>
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

        services.AddHttpClient<IAiAnswerScoringClient, AiAnswerScoringClient>(client =>
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

        services.AddHttpClient<IAiCandidateFeedbackClient, AiCandidateFeedbackClient>(client =>
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

        // Keyed UnitOfWork
        services.AddKeyedScoped<IUnitOfWork, ScreeningUnitOfWork>("screening");

        // Validators
        services.AddValidatorsFromAssemblyContaining<SubmitAssessmentRequestValidator>();

        return services;
    }
}
