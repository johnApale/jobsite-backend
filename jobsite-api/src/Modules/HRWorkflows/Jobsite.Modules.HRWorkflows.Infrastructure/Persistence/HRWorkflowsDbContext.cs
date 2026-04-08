using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;

public sealed class HRWorkflowsDbContext : TenantDbContext
{
    public HRWorkflowsDbContext(DbContextOptions<HRWorkflowsDbContext> options,
        IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }

    public DbSet<FinalInterview> FinalInterviews => Set<FinalInterview>();
    public DbSet<InterviewPanelist> InterviewPanelists => Set<InterviewPanelist>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HRWorkflowsDbContext).Assembly);
    }
}
