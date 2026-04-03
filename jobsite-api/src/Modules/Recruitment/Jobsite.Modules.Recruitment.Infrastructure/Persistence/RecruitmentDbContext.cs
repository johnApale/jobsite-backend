using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Recruitment module — maps to the <c>recruitment</c> schema.
/// Full configuration applied in Phase C (entity configurations, DbSets).
/// </summary>
public sealed class RecruitmentDbContext : TenantDbContext
{
    public RecruitmentDbContext(DbContextOptions<RecruitmentDbContext> options,
        IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecruitmentDbContext).Assembly);
    }
}
