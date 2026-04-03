using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence;

/// <summary>
/// Factory for creating per-tenant <see cref="RecruitmentDbContext"/> instances.
/// Resolves connection strings from HTTP context or explicit parameters.
/// </summary>
public sealed class TenantRecruitmentDbContextFactory : ITenantDbContextFactory<RecruitmentDbContext>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDomainEventDispatcher? _dispatcher;

    public TenantRecruitmentDbContextFactory(
        IHttpContextAccessor httpContextAccessor, IDomainEventDispatcher? dispatcher = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _dispatcher = dispatcher;
    }

    public RecruitmentDbContext CreateDbContext()
    {
        string? connectionString = _httpContextAccessor.HttpContext?.Items["TenantConnectionString"] as string;

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                "Tenant connection string not found. Ensure TenantResolutionMiddleware resolved the tenant.");

        return CreateDbContext(connectionString);
    }

    public RecruitmentDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<RecruitmentDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new RecruitmentDbContext(optionsBuilder.Options, _dispatcher);
    }
}
