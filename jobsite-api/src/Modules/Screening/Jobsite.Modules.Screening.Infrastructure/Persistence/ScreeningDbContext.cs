using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence;

public sealed class ScreeningDbContext : TenantDbContext
{
    public ScreeningDbContext(DbContextOptions<ScreeningDbContext> options,
        IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }

    public DbSet<ScreeningResult> ScreeningResults => Set<ScreeningResult>();
    public DbSet<ScreeningQuestionResponse> ScreeningQuestionResponses => Set<ScreeningQuestionResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScreeningDbContext).Assembly);
    }
}
