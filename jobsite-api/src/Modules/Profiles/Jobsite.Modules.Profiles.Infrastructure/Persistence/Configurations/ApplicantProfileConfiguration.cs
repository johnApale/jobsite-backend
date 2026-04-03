using Jobsite.Modules.Profiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>profiles.applicant_profiles</c> table.
/// Shared PK with <c>auth.users</c> — the Id column is both PK and FK.
/// Cross-schema FK added via raw SQL in the migration.
/// </summary>
public sealed class ApplicantProfileConfiguration : IEntityTypeConfiguration<ApplicantProfile>
{
    public void Configure(EntityTypeBuilder<ApplicantProfile> builder)
    {
        builder.ToTable("applicant_profiles", "profiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("user_id");

        builder.Property(p => p.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Phone)
            .HasMaxLength(30);

        builder.Property(p => p.City)
            .HasMaxLength(100);

        builder.Property(p => p.Country)
            .HasMaxLength(100);

        builder.Property(p => p.Skills)
            .HasColumnType("jsonb");

        builder.Property(p => p.SocialLinks)
            .HasColumnType("jsonb");

        builder.Property(p => p.Documents)
            .HasColumnType("jsonb");

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(p => new { p.City, p.Country })
            .HasDatabaseName("ix_applicant_profiles_city_country");

        // GIN index on skills JSONB — added via raw SQL in migration
        // because EF Core doesn't natively support GIN indexes.

        // One-to-many with Resume
        builder.HasMany(p => p.Resumes)
            .WithOne(r => r.ApplicantProfile)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events from AggregateRoot
        builder.Ignore(p => p.DomainEvents);
    }
}
