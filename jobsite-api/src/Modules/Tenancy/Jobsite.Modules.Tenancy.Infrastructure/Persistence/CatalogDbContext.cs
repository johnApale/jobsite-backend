using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// DbContext for the shared Catalog database.
/// Manages tenant metadata, branding, and routing info.
/// Registered as a singleton factory — the catalog DB is shared across all tenants.
/// </summary>
public sealed class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
