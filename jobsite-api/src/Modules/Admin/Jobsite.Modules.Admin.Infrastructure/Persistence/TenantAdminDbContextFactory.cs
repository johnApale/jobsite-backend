using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// Factory for creating per-tenant <see cref="AdminDbContext"/> instances.
/// Resolves connection strings from HTTP context or explicit parameters.
/// </summary>
public sealed class TenantAdminDbContextFactory : ITenantDbContextFactory<AdminDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantAdminDbContextFactory(IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public AdminDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public AdminDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<AdminDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AdminDbContext(optionsBuilder.Options, _dispatcher);
    }
}
