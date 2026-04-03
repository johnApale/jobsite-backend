using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// Per-tenant DbContext for the Admin module.
/// Manages company settings and audit logs in the <c>admin</c> schema.
/// </summary>
public sealed class AdminDbContext : TenantDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher)
    {
    }

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
    }
}
