using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence;

/// <summary>
/// Factory for creating per-tenant <see cref="AuthDbContext"/> instances.
/// Resolves connection strings from HTTP context or explicit parameters.
/// </summary>
public sealed class TenantAuthDbContextFactory : ITenantDbContextFactory<AuthDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantAuthDbContextFactory(IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public AuthDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public AuthDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<AuthDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AuthDbContext(optionsBuilder.Options, _dispatcher);
    }
}
