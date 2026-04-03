using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>recruitment.job_postings</c> table.
/// Cross-schema FK to <c>auth.users</c> for <c>posted_by</c> added via raw SQL in migration.
/// </summary>
public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("job_postings", "recruitment", t =>
        {
            t.HasCheckConstraint(
                "chk_job_postings_status",
                $"status IN ('{JobPostingStatus.Draft}', '{JobPostingStatus.Published}', '{JobPostingStatus.Closed}')");

            t.HasCheckConstraint(
                "chk_job_postings_location_type",
                $"location_type IN ('{LocationType.OnSite}', '{LocationType.Remote}', '{LocationType.Hybrid}')");

            t.HasCheckConstraint(
                "chk_job_postings_employment_type",
                $"employment_type IN ('{EmploymentType.FullTime}', '{EmploymentType.PartTime}', '{EmploymentType.Contract}', '{EmploymentType.Temporary}', '{EmploymentType.Internship}')");
        });

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(j => j.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(j => j.Description)
            .IsRequired();

        builder.Property(j => j.LocationType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.City)
            .HasMaxLength(100);

        builder.Property(j => j.Country)
            .HasMaxLength(100);

        builder.Property(j => j.EmploymentType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.SalaryMin)
            .HasColumnType("decimal(12,2)");

        builder.Property(j => j.SalaryMax)
            .HasColumnType("decimal(12,2)");

        builder.Property(j => j.SalaryCurrency)
            .HasMaxLength(3);

        builder.Property(j => j.Department)
            .HasMaxLength(100);

        builder.Property(j => j.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.PostedBy)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(j => j.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(j => j.ClientCompanyId)
            .HasDatabaseName("ix_job_postings_client_company");

        builder.HasIndex(j => j.Status)
            .HasDatabaseName("ix_job_postings_status");

        builder.HasIndex(j => j.PostedBy)
            .HasDatabaseName("ix_job_postings_posted_by");

        builder.HasIndex(j => j.PublishedAt)
            .HasDatabaseName("ix_job_postings_published_at");

        builder.HasIndex(j => new { j.City, j.Country })
            .HasDatabaseName("ix_job_postings_location");

        // One-to-many with Criteria
        builder.HasMany(j => j.Criteria)
            .WithOne(c => c.JobPosting)
            .HasForeignKey(c => c.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many with Questions
        builder.HasMany(j => j.Questions)
            .WithOne(q => q.JobPosting)
            .HasForeignKey(q => q.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many with Applications
        builder.HasMany(j => j.Applications)
            .WithOne(a => a.JobPosting)
            .HasForeignKey(a => a.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events from AggregateRoot
        builder.Ignore(j => j.DomainEvents);
    }
}
