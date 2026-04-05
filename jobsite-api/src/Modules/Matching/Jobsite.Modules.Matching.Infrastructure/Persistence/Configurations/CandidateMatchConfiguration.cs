using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence.Configurations;

public sealed class CandidateMatchConfiguration : IEntityTypeConfiguration<CandidateMatch>
{
    public void Configure(EntityTypeBuilder<CandidateMatch> builder)
    {
        builder.ToTable("candidate_matches", "matching", t =>
        {
            t.HasCheckConstraint("chk_matches_match_strength",
                $"match_strength IS NULL OR match_strength IN ('{MatchStrength.Strong}', '{MatchStrength.Good}', '{MatchStrength.Moderate}', '{MatchStrength.Weak}')");
        });

        // Shared PK with recruitment.applications
        builder.HasKey(m => m.ApplicationId);
        builder.Property(m => m.ApplicationId)
            .ValueGeneratedNever();

        builder.Property(m => m.JobPostingId)
            .IsRequired();

        builder.Property(m => m.ApplicantUserId)
            .IsRequired();

        builder.Property(m => m.ScreeningScore)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(m => m.AssessmentScore)
            .HasColumnType("decimal(5,2)");

        builder.Property(m => m.CompositeScore)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(m => m.MatchStrength)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Rank);

        builder.Property(m => m.ScreeningCompletedAt)
            .IsRequired();

        builder.Property(m => m.AssessmentCompletedAt);

        builder.Property(m => m.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Indexes
        builder.HasIndex(m => m.JobPostingId)
            .HasDatabaseName("ix_candidate_matches_job_posting_id");

        builder.HasIndex(m => m.ApplicantUserId)
            .HasDatabaseName("ix_candidate_matches_applicant_user_id");

        builder.HasIndex(m => m.CompositeScore)
            .HasDatabaseName("ix_candidate_matches_composite_score");

        builder.HasIndex(m => m.MatchStrength)
            .HasDatabaseName("ix_candidate_matches_match_strength");

        // Ignore Entity.Id — we use ApplicationId as PK
        builder.Ignore(m => m.Id);

        // Note: Cross-schema FK to recruitment.applications is added via raw SQL in migration.
        // Note: Cross-schema FK to recruitment.job_postings is added via raw SQL in migration.
        // Note: Cross-schema FK to auth.users is added via raw SQL in migration.
    }
}
