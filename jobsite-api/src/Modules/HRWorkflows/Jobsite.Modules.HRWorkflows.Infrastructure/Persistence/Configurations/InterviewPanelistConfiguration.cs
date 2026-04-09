using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Configurations;

public sealed class InterviewPanelistConfiguration : IEntityTypeConfiguration<InterviewPanelist>
{
    public void Configure(EntityTypeBuilder<InterviewPanelist> builder)
    {
        builder.ToTable("interview_panelists", "hr_workflows", t =>
        {
            t.HasCheckConstraint("chk_panelists_recommendation",
                $"recommendation IS NULL OR recommendation IN ('{InterviewRecommendation.StrongHire}', '{InterviewRecommendation.Hire}', '{InterviewRecommendation.NoHire}', '{InterviewRecommendation.StrongNoHire}')");

            t.HasCheckConstraint("chk_panelists_rating",
                "rating IS NULL OR (rating >= 1.0 AND rating <= 5.0)");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.InterviewId)
            .IsRequired();

        builder.Property(p => p.InterviewerId)
            .IsRequired();

        builder.Property(p => p.Rating)
            .HasColumnType("decimal(3,1)");

        builder.Property(p => p.Recommendation)
            .HasMaxLength(20);

        builder.Property(p => p.Strengths);

        builder.Property(p => p.Concerns);

        builder.Property(p => p.Notes);

        builder.Property(p => p.FeedbackSubmittedAt);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Unique constraint: one feedback per interviewer per interview
        builder.HasIndex(p => new { p.InterviewId, p.InterviewerId })
            .IsUnique()
            .HasDatabaseName("uq_panelists_interview_interviewer");

        // Indexes
        builder.HasIndex(p => p.InterviewId)
            .HasDatabaseName("ix_panelists_interview_id");

        builder.HasIndex(p => p.InterviewerId)
            .HasDatabaseName("ix_panelists_interviewer_id");

        builder.HasIndex(p => new { p.InterviewId, p.FeedbackSubmittedAt })
            .HasDatabaseName("ix_panelists_feedback_pending");

    }
}
