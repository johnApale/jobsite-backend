using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence.Configurations;

public sealed class ShortlistCandidateConfiguration : IEntityTypeConfiguration<ShortlistCandidate>
{
    public void Configure(EntityTypeBuilder<ShortlistCandidate> builder)
    {
        builder.ToTable("shortlist_candidates", "matching", t =>
        {
            t.HasCheckConstraint("chk_shortlist_candidates_source",
                $"source IN ('{ShortlistCandidateSource.Algorithm}', '{ShortlistCandidateSource.Manual}')");
            t.HasCheckConstraint("chk_shortlist_candidates_status",
                $"status IN ('{ShortlistCandidateStatus.Pending}', '{ShortlistCandidateStatus.Approved}', '{ShortlistCandidateStatus.Rejected}')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.ShortlistId)
            .IsRequired();

        builder.Property(c => c.ApplicationId)
            .IsRequired();

        builder.Property(c => c.ApplicantUserId)
            .IsRequired();

        builder.Property(c => c.CompositeScore)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(c => c.Rank)
            .IsRequired();

        builder.Property(c => c.Source)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasMaxLength(20)
            .HasDefaultValue(ShortlistCandidateStatus.Pending)
            .IsRequired();

        builder.Property(c => c.AddedAt)
            .IsRequired();

        builder.Property(c => c.RemovedAt);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Unique constraint: one entry per candidate per shortlist
        builder.HasIndex(c => new { c.ShortlistId, c.ApplicationId })
            .IsUnique()
            .HasDatabaseName("uq_shortlist_candidates_shortlist_app");

        // Indexes
        builder.HasIndex(c => c.ApplicationId)
            .HasDatabaseName("ix_shortlist_candidates_application_id");

    }
}
