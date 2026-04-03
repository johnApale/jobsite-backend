using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence;

/// <summary>
/// Per-tenant DbContext for the Profiles module.
/// Manages applicant profiles and resumes in the <c>profiles</c> schema.
/// </summary>
public sealed class ProfilesDbContext : TenantDbContext
{
    public ProfilesDbContext(DbContextOptions<ProfilesDbContext> options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher)
    {
    }

    public DbSet<ApplicantProfile> ApplicantProfiles => Set<ApplicantProfile>();
    public DbSet<Resume> Resumes => Set<Resume>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfilesDbContext).Assembly);
    }
}
