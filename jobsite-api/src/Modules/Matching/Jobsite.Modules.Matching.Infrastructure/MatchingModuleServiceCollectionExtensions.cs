using FluentValidation;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Application.Validators;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.Modules.Matching.Infrastructure.Persistence;
using Jobsite.Modules.Matching.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Matching.Infrastructure;

public static class MatchingModuleServiceCollectionExtensions
{
    public static IServiceCollection AddMatchingModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory + scoped resolution
        services.AddScoped<ITenantDbContextFactory<MatchingDbContext>, TenantMatchingDbContextFactory>();
        services.AddScoped<MatchingDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<MatchingDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<ICandidateMatchRepository, CandidateMatchRepository>();
        services.AddScoped<IShortlistRepository, ShortlistRepository>();

        // Application services
        services.AddScoped<IScoreAggregationService, ScoreAggregationService>();
        services.AddScoped<IMatchingService, MatchingService>();
        services.AddScoped<IShortlistService, ShortlistService>();

        // Keyed UnitOfWork
        services.AddKeyedScoped<IUnitOfWork, MatchingUnitOfWork>("matching");

        // Validators
        services.AddValidatorsFromAssemblyContaining<GenerateShortlistRequestValidator>();

        return services;
    }
}
