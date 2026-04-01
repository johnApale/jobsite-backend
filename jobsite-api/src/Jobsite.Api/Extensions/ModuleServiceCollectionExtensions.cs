using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jobsite.Api.Configuration;
using Jobsite.Modules.Tenancy.Infrastructure;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
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

        // 4. MediatR (scans all module assemblies)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CatalogDbContext>();
            // Add other module assemblies here as they are implemented
        });

        // 5. Module registrations
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
