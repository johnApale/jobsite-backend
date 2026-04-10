using FluentValidation;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Application.Validators;
using Jobsite.Modules.Screening.Infrastructure.CrossModule;
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
        services.AddScoped<IDeterministicScoringEngine, DeterministicScoringEngine>();

        // Keyed UnitOfWork
        services.AddKeyedScoped<IUnitOfWork, ScreeningUnitOfWork>("screening");

        // Cross-module readers (consumed by Matching module)
        services.AddScoped<IScreeningScoreReader, ScreeningScoreReader>();
        services.AddScoped<IScreeningStatsReader, ScreeningStatsReader>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<SubmitAssessmentRequestValidator>();

        return services;
    }
}
