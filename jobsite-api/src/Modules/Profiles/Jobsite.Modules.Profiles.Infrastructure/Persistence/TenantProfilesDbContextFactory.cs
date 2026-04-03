using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence;

/// <summary>
/// Factory for creating per-tenant <see cref="ProfilesDbContext"/> instances.
/// Resolves connection strings from HTTP context or explicit parameters.
/// </summary>
public sealed class TenantProfilesDbContextFactory : ITenantDbContextFactory<ProfilesDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantProfilesDbContextFactory(IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public ProfilesDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public ProfilesDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<ProfilesDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new ProfilesDbContext(optionsBuilder.Options, _dispatcher);
    }
}
