using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Application.Services;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Infrastructure.Repositories;
using Jobsite.Modules.Auth.Infrastructure.Security;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Auth.Infrastructure;

/// <summary>
/// DI registration for the Auth module.
/// Called from <c>ModuleServiceCollectionExtensions.AddJobsiteModules()</c>.
/// </summary>
public static class AuthModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAuthModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        JwtSettings jwtSettings = configuration
            .GetSection("App")
            .Get<JwtSettings>() ?? new JwtSettings();

        // DbContext factory for per-tenant database resolution
        services.AddScoped<ITenantDbContextFactory<AuthDbContext>, TenantAuthDbContextFactory>();
        services.AddScoped<AuthDbContext>(sp =>
            sp.GetRequiredService<ITenantDbContextFactory<AuthDbContext>>().CreateDbContext());

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Unit of Work (scoped to Auth tenant DB, keyed for disambiguation)
        services.AddKeyedScoped<IUnitOfWork, AuthUnitOfWork>("auth");

        // Security
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtService>(_ => new JwtService(jwtSettings));
        services.AddScoped<IOAuthProviderValidator, StubOAuthProviderValidator>();

        // Services
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
