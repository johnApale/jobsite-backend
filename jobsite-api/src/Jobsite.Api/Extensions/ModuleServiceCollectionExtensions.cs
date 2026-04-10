using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
            bus.AddConsumers(typeof(ScreeningDbContext).Assembly);

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

        // 9a. Tenant ID provider (for Application services that publish integration events)
        services.AddScoped<ITenantIdProvider, HttpContextTenantIdProvider>();

        // 10. Tenant connection resolver (for consumers and background services)
        services.AddScoped<ITenantConnectionResolver, CatalogTenantConnectionResolver>();

        // 11. Distributed cache (Redis or in-memory fallback)
        if (!string.IsNullOrWhiteSpace(appSettings.Redis.ConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
                options.Configuration = appSettings.Redis.ConnectionString);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // 12. CORS
        services.AddCors(options =>
        {
            options.AddPolicy("TenantPolicy", policy =>
            {
                if (appSettings.Cors.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(appSettings.Cors.AllowedOrigins);
                }
                else
                {
                    // Development: allow any origin with credentials requires explicit origins,
                    // so allow common dev patterns
                    policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost"
                        || origin.EndsWith(".djobsite.com", StringComparison.OrdinalIgnoreCase));
                }

                policy.WithMethods("GET", "POST", "PATCH", "DELETE", "OPTIONS")
                      .WithHeaders("Authorization", "Content-Type", "X-Correlation-ID")
                      .WithExposedHeaders("X-Correlation-ID", "X-RateLimit-Limit",
                          "X-RateLimit-Remaining", "X-RateLimit-Reset")
                      .AllowCredentials();
            });
        });

        // 13. Rate limiting
        RateLimitSettings rateLimits = appSettings.RateLimiting;
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                string requestId = context.HttpContext.Items["CorrelationId"]?.ToString()
                    ?? context.HttpContext.TraceIdentifier;

                context.HttpContext.Response.Headers["X-RateLimit-Limit"] = "0";
                context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    context.HttpContext.Response.Headers["X-RateLimit-Reset"] =
                        retryAfter.TotalSeconds.ToString("F0");
                }

                await context.HttpContext.Response.WriteAsync(
                    $$"""{"code":"RATE_LIMITED","message":"Rate limit exceeded","request_id":"{{requestId}}"}""",
                    ct);
            };

            // Global per-tenant policy
            options.AddPolicy("global", httpContext =>
            {
                string partitionKey = httpContext.User.FindFirst("tenant_id")?.Value
                    ?? httpContext.Items["Tenant"]?.ToString()
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimits.GlobalRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            // Auth per-IP policy (brute-force protection)
            options.AddPolicy("auth", httpContext =>
            {
                string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimits.AuthRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            // AI endpoints per-tenant policy
            options.AddPolicy("ai", httpContext =>
            {
                string partitionKey = httpContext.User.FindFirst("tenant_id")?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimits.AiRequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
        });

        // 14. Health checks
        string catalogConnectionString = configuration.GetConnectionString("CatalogDb") ?? string.Empty;
        Uri rabbitMqUri = new($"amqp://{appSettings.MessageBroker.Username}:{appSettings.MessageBroker.Password}@{appSettings.MessageBroker.Host}:{appSettings.MessageBroker.Port}{appSettings.MessageBroker.VirtualHost}");
        IHealthChecksBuilder healthChecks = services.AddHealthChecks()
            .AddNpgSql(catalogConnectionString, name: "postgres", tags: new[] { "readiness" })
            .AddRabbitMQ(sp =>
            {
                RabbitMQ.Client.ConnectionFactory factory = new() { Uri = rabbitMqUri };
                return factory.CreateConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
            }, name: "rabbitmq", tags: new[] { "readiness" })
            .AddUrlGroup(
                new Uri($"{appSettings.AiServiceUrl}/health"),
                name: "ai-service",
                tags: new[] { "readiness" });

        if (!string.IsNullOrWhiteSpace(appSettings.Redis.ConnectionString))
        {
            healthChecks.AddRedis(appSettings.Redis.ConnectionString, name: "redis", tags: ["readiness"]);
        }

        // 15. OpenTelemetry (tracing + metrics)
        if (!string.IsNullOrWhiteSpace(appSettings.OpenTelemetry.OtlpEndpoint))
        {
            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(serviceName: appSettings.OpenTelemetry.ServiceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddOtlpExporter(opts => opts.Endpoint = new Uri(appSettings.OpenTelemetry.OtlpEndpoint)))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(opts => opts.Endpoint = new Uri(appSettings.OpenTelemetry.OtlpEndpoint)));
        }

        // 16. Module registrations
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
