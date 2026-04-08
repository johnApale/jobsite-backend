using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;

public sealed class TenantHRWorkflowsDbContextFactory : ITenantDbContextFactory<HRWorkflowsDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantHRWorkflowsDbContextFactory(
        IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public HRWorkflowsDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public HRWorkflowsDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<HRWorkflowsDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new HRWorkflowsDbContext(optionsBuilder.Options, _dispatcher);
    }
}
