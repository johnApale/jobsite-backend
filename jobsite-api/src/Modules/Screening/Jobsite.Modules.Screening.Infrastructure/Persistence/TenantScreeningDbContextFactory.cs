using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence;

public sealed class TenantScreeningDbContextFactory : ITenantDbContextFactory<ScreeningDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantScreeningDbContextFactory(
        IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public ScreeningDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public ScreeningDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<ScreeningDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new ScreeningDbContext(optionsBuilder.Options, _dispatcher);
    }
}
