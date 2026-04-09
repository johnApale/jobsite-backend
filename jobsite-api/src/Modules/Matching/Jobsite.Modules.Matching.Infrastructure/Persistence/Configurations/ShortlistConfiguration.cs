using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence.Configurations;

public sealed class ShortlistConfiguration : IEntityTypeConfiguration<Shortlist>
{
    public void Configure(EntityTypeBuilder<Shortlist> builder)
    {
        builder.ToTable("shortlists", "matching", t =>
        {
            t.HasCheckConstraint("chk_shortlists_status",
                $"status IN ('{ShortlistStatus.Draft}', '{ShortlistStatus.Finalized}')");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.JobPostingId)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.GeneratedBy)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.TotalCandidates)
            .IsRequired();

        builder.Property(s => s.FinalizedAt);

        builder.Property(s => s.FinalizedBy);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Indexes
        builder.HasIndex(s => s.JobPostingId)
            .HasDatabaseName("ix_shortlists_job_posting_id");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_shortlists_status");

        // Navigation
        builder.HasMany(s => s.Candidates)
            .WithOne(c => c.Shortlist)
            .HasForeignKey(c => c.ShortlistId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
