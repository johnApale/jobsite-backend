using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>recruitment.client_companies</c> table.
/// </summary>
public sealed class ClientCompanyConfiguration : IEntityTypeConfiguration<ClientCompany>
{
    public void Configure(EntityTypeBuilder<ClientCompany> builder)
    {
        builder.ToTable("client_companies", "recruitment", t =>
        {
            t.HasCheckConstraint(
                "chk_client_companies_status",
                $"status IN ('{ClientCompanyStatus.Active}', '{ClientCompanyStatus.Inactive}')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.DisplayName)
            .HasMaxLength(200);

        builder.Property(c => c.IsAnonymous)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.Industry)
            .HasMaxLength(100);

        builder.Property(c => c.Website)
            .HasMaxLength(2048);

        builder.Property(c => c.ContactName)
            .HasMaxLength(200);

        builder.Property(c => c.ContactEmail)
            .HasMaxLength(254);

        builder.Property(c => c.ContactPhone)
            .HasMaxLength(20);

        builder.Property(c => c.Notes);

        builder.Property(c => c.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(c => c.Name)
            .HasDatabaseName("ix_client_companies_name");

        builder.HasIndex(c => c.Status)
            .HasDatabaseName("ix_client_companies_status");

        // One-to-many with JobPosting
        builder.HasMany(c => c.JobPostings)
            .WithOne(j => j.ClientCompany)
            .HasForeignKey(j => j.ClientCompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ignore domain events from AggregateRoot
        builder.Ignore(c => c.DomainEvents);
    }
}
