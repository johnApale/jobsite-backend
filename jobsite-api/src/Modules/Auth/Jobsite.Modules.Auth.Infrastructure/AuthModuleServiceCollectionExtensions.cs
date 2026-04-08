using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Application.Services;
using Jobsite.Modules.Auth.Infrastructure.Email;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Infrastructure.Repositories;
using Jobsite.Modules.Auth.Infrastructure.Security;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddSingleton(jwtSettings);
        services.AddScoped<IEmailService, StubEmailService>();

        // OAuth: use real provider dispatcher in production, stub in development
        services.AddHttpClient<GoogleOAuthValidator>();
        services.AddHttpClient<AppleOAuthValidator>();
        services.AddHttpClient<FacebookOAuthValidator>();
        services.AddScoped(sp =>
        {
            IHostEnvironment env = sp.GetRequiredService<IHostEnvironment>();
            if (env.IsDevelopment())
                return (IOAuthProviderValidator)sp.GetRequiredService<StubOAuthProviderValidator>();
            return sp.GetRequiredService<OAuthProviderDispatcher>();
        });
        services.AddScoped<StubOAuthProviderValidator>();
        services.AddScoped<OAuthProviderDispatcher>();

        // Services
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
