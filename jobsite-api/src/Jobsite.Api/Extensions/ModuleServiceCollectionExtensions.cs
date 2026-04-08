using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobsite.Api.Behaviors;
using Jobsite.Api.Configuration;
using Jobsite.Api.Infrastructure;
using Jobsite.Modules.Tenancy.Infrastructure;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Api;
using Jobsite.Modules.Auth.Application.Services;
using Jobsite.Modules.Auth.Infrastructure;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.Admin.Application.Services;
using Jobsite.Modules.Admin.Infrastructure;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Infrastructure;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Recruitment.Infrastructure;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.Modules.Screening.Infrastructure;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.Modules.Matching.Infrastructure;
using Jobsite.Modules.Matching.Infrastructure.Persistence;
using Jobsite.Modules.HRWorkflows.Infrastructure;
using Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;

namespace Jobsite.Api.Extensions;

/// <summary>
/// Aggregates all module DI registrations and cross-cutting infrastructure.
/// Called from <c>Program.cs</c>.
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    public static IServiceCollection AddJobsiteModules(
        this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Global JSON defaults (snake_case, omit nulls)
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        // 2. App settings
        AppSettings appSettings = configuration
            .GetSection(AppSettings.SectionName)
            .Get<AppSettings>() ?? new AppSettings();

        // 3. JWT authentication
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = appSettings.JwtIssuer,
                    ValidAudience = appSettings.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(appSettings.JwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorizationBuilder()
            .AddAuthModulePolicies();

        // 4. HttpContext accessor (needed by ITenantDbContextFactory for connection string resolution)
        services.AddHttpContextAccessor();

        // 5. In-process domain event bus (scans all module assemblies)
        services.AddDomainEventBus(
            typeof(CatalogDbContext).Assembly,
            typeof(AuthDbContext).Assembly,
            typeof(AdminDbContext).Assembly,
            typeof(ProfilesDbContext).Assembly,
            typeof(RecruitmentDbContext).Assembly,
            typeof(ScreeningDbContext).Assembly,
            typeof(MatchingDbContext).Assembly,
            typeof(HRWorkflowsDbContext).Assembly);

        // 6. FluentValidation validators (assembly scanning)
        services.AddValidatorsFromAssemblyContaining<CatalogDbContext>(includeInternalTypes: true);
        services.AddValidatorsFromAssemblyContaining<AuthService>(includeInternalTypes: true);
        services.AddValidatorsFromAssemblyContaining<AdminSettingsService>(includeInternalTypes: true);

        // 8. MassTransit + RabbitMQ (message broker for integration events)
        services.AddMassTransit(bus =>
        {
            // Add consumers from module assemblies
            bus.AddConsumers(typeof(ProfilesDbContext).Assembly);

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(appSettings.MessageBroker.Host, (ushort)appSettings.MessageBroker.Port,
                    appSettings.MessageBroker.VirtualHost, h =>
                    {
                        h.Username(appSettings.MessageBroker.Username);
                        h.Password(appSettings.MessageBroker.Password);
                    });

                // snake_case JSON serialization for Python AI Service interop
                cfg.ConfigureJsonSerializerOptions(options =>
                {
                    options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    return options;
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        // 9. Integration event publisher (wraps MassTransit IPublishEndpoint)
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        // 10. Tenant connection resolver (for consumers and background services)
        services.AddScoped<ITenantConnectionResolver, CatalogTenantConnectionResolver>();

        // 11. Module registrations
        services.AddTenancyModule(configuration);
        services.AddAuthModule(configuration);
        services.AddAdminModule(configuration);
        services.AddProfilesModule(configuration);
        services.AddRecruitmentModule(configuration);
        services.AddScreeningModule(configuration);
        services.AddMatchingModule(configuration);
        services.AddHRWorkflowsModule(configuration);

        return services;
    }

    /// <summary>
    /// Registers all <see cref="IDomainEventHandler{T}"/> implementations from the given assemblies,
    /// the domain event pipeline behaviors, and the in-process dispatcher.
    /// </summary>
    private static void AddDomainEventBus(
        this IServiceCollection services, params Assembly[] assemblies)
    {
        // Register all IDomainEventHandler<T> implementations as scoped
        Type openHandlerType = typeof(IDomainEventHandler<>);

        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                IEnumerable<Type> handlerInterfaces = type.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openHandlerType);

                foreach (Type handlerInterface in handlerInterfaces)
                {
                    services.AddScoped(handlerInterface, type);
                }
            }
        }

        // Pipeline behaviors (order matters: logging wraps validation wraps handlers)
        services.AddScoped<IDomainEventPipelineBehavior, LoggingEventBehavior>();
        services.AddScoped<IDomainEventPipelineBehavior, ValidationEventBehavior>();

        // Domain event dispatcher
        services.AddScoped<IDomainEventDispatcher, InProcessDomainEventDispatcher>();
    }
}
