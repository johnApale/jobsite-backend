using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence.Configurations;

public sealed class ScreeningResultConfiguration : IEntityTypeConfiguration<ScreeningResult>
{
    public void Configure(EntityTypeBuilder<ScreeningResult> builder)
    {
        builder.ToTable("screening_results", "screening", t =>
        {
            t.HasCheckConstraint("chk_screening_results_status",
                $"status IN ('{ScreeningStatus.Pending}', '{ScreeningStatus.InProgress}', '{ScreeningStatus.Completed}', '{ScreeningStatus.Failed}')");

            t.HasCheckConstraint("chk_screening_results_match_strength",
                $"match_strength IS NULL OR match_strength IN ('{MatchStrength.Strong}', '{MatchStrength.Good}', '{MatchStrength.Moderate}', '{MatchStrength.Weak}')");

            t.HasCheckConstraint("chk_screening_results_outcome",
                $"outcome IS NULL OR outcome IN ('{ScreeningOutcome.AutoAdvanced}', '{ScreeningOutcome.AutoRejected}', '{ScreeningOutcome.ManualReview}', '{ScreeningOutcome.ManuallyAdvanced}', '{ScreeningOutcome.ManuallyRejected}')");
        });

        // Shared PK with recruitment.applications
        builder.HasKey(r => r.ApplicationId);
        builder.Property(r => r.ApplicationId)
            .ValueGeneratedNever(); // Shared key — set externally

        builder.Property(r => r.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.OverallScore)
            .HasColumnType("decimal(5,2)");

        builder.Property(r => r.MatchStrength)
            .HasMaxLength(20);

        builder.Property(r => r.Outcome)
            .HasMaxLength(20);

        builder.Property(r => r.CriteriaScoreBreakdown)
            .HasColumnType("jsonb");

        builder.Property(r => r.AiCriteriaScoreBreakdown)
            .HasColumnType("jsonb");

        builder.Property(r => r.AiOverallScore)
            .HasColumnType("decimal(5,2)");

        builder.Property(r => r.QuestionScoreBreakdown)
            .HasColumnType("jsonb");

        builder.Property(r => r.AssessmentScore)
            .HasColumnType("decimal(5,2)");

        builder.Property(r => r.CandidateFeedback)
            .HasColumnType("text");

        builder.Property(r => r.AutoAdvanceThreshold)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(r => r.AutoRejectThreshold)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(r => r.ReviewNotes)
            .HasColumnType("text");

        builder.Property(r => r.FailureReason)
            .HasMaxLength(500);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Indexes
        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_screening_results_status");

        builder.HasIndex(r => r.MatchStrength)
            .HasDatabaseName("ix_screening_results_match_strength");

        builder.HasIndex(r => r.Outcome)
            .HasDatabaseName("ix_screening_results_outcome");

        builder.HasIndex(r => r.OverallScore)
            .HasDatabaseName("ix_screening_results_overall_score");

        // Note: Cross-schema FK to recruitment.applications is added via raw SQL in migration.
        // Note: Cross-schema FK to auth.users for reviewed_by is added via raw SQL in migration.

        // Ignore Entity.Id — we use ApplicationId as PK
        builder.Ignore(r => r.Id);
    }
}
