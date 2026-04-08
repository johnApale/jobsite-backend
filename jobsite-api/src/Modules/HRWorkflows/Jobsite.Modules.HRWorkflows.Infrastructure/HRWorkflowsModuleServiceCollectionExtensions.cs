using FluentValidation;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.HRWorkflows.Application.Validators;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;
using Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.HRWorkflows.Infrastructure;

public static class HRWorkflowsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddHRWorkflowsModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext factory + scoped resolution
        services.AddScoped<ITenantDbContextFactory<HRWorkflowsDbContext>, TenantHRWorkflowsDbContextFactory>();
        services.AddScoped<HRWorkflowsDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<HRWorkflowsDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<IFinalInterviewRepository, FinalInterviewRepository>();
        services.AddScoped<IJobOfferRepository, JobOfferRepository>();

        // Application services
        services.AddScoped<IFeedbackAggregationService, FeedbackAggregationService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IOfferService, OfferService>();

        // Keyed UnitOfWork
        services.AddKeyedScoped<IUnitOfWork, HRWorkflowsUnitOfWork>("hr_workflows");

        // Validators
        services.AddValidatorsFromAssemblyContaining<ScheduleInterviewRequestValidator>();

        return services;
    }
}
