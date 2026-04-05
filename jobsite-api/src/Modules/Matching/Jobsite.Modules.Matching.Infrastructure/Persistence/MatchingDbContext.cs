using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence;

public sealed class MatchingDbContext : TenantDbContext
{
    public MatchingDbContext(DbContextOptions<MatchingDbContext> options,
        IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher) { }

    public DbSet<CandidateMatch> CandidateMatches => Set<CandidateMatch>();
    public DbSet<Shortlist> Shortlists => Set<Shortlist>();
    public DbSet<ShortlistCandidate> ShortlistCandidates => Set<ShortlistCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MatchingDbContext).Assembly);
    }
}
