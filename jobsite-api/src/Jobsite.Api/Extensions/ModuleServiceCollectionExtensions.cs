using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobsite.Api.Behaviors;
using Jobsite.Api.Configuration;
using Jobsite.Api.Infrastructure;
using Jobsite.Modules.Tenancy.Infrastructure;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

        services.AddAuthorization();

        // 4. HttpContext accessor (needed by ITenantDbContextFactory for connection string resolution)
        services.AddHttpContextAccessor();

        // 5. MediatR (scans all module assemblies)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CatalogDbContext>();
            // Add other module assemblies here as they are implemented

            cfg.AddOpenBehavior(typeof(LoggingPipelineBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        // 6. FluentValidation validators (assembly scanning)
        services.AddValidatorsFromAssemblyContaining<CatalogDbContext>(includeInternalTypes: true);

        // 7. Domain event dispatcher (bridges SharedKernel → MediatR)
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        // 8. MassTransit + RabbitMQ (message broker for integration events)
        services.AddMassTransit(bus =>
        {
            // Add consumers from module assemblies as they are implemented
            // bus.AddConsumers(typeof(CatalogDbContext).Assembly);

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

        // 10. Module registrations
        services.AddTenancyModule(configuration);
        // services.AddAuthModule(configuration);
        // services.AddAdminModule(configuration);
        // services.AddProfilesModule(configuration);
        // services.AddRecruitmentModule(configuration);
        // services.AddScreeningModule(configuration);
        // services.AddMatchingModule(configuration);
        // services.AddHRWorkflowsModule(configuration);

        return services;
    }
}
