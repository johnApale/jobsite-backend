using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Configurations;

public sealed class FinalInterviewConfiguration : IEntityTypeConfiguration<FinalInterview>
{
    public void Configure(EntityTypeBuilder<FinalInterview> builder)
    {
        builder.ToTable("final_interviews", "hr_workflows", t =>
        {
            t.HasCheckConstraint("chk_final_interviews_status",
                $"status IN ('{InterviewStatus.Scheduled}', '{InterviewStatus.InProgress}', '{InterviewStatus.Completed}', '{InterviewStatus.Cancelled}', '{InterviewStatus.NoShow}')");

            t.HasCheckConstraint("chk_final_interviews_interview_type",
                $"interview_type IN ('{InterviewType.InPerson}', '{InterviewType.Video}', '{InterviewType.Phone}')");

            t.HasCheckConstraint("chk_final_interviews_recommendation",
                $"overall_recommendation IS NULL OR overall_recommendation IN ('{InterviewRecommendation.StrongHire}', '{InterviewRecommendation.Hire}', '{InterviewRecommendation.NoHire}', '{InterviewRecommendation.StrongNoHire}')");
        });

        // Shared PK with recruitment.applications
        builder.HasKey(i => i.ApplicationId);
        builder.Property(i => i.ApplicationId)
            .ValueGeneratedNever();

        builder.Property(i => i.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.InterviewType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.ScheduledAt)
            .IsRequired();

        builder.Property(i => i.DurationMinutes)
            .HasDefaultValue(60)
            .IsRequired();

        builder.Property(i => i.Location)
            .HasMaxLength(500);

        builder.Property(i => i.ScheduledBy)
            .IsRequired();

        builder.Property(i => i.OverallRecommendation)
            .HasMaxLength(20);

        builder.Property(i => i.DecisionNotes);

        builder.Property(i => i.DecidedBy);

        builder.Property(i => i.DecidedAt);

        builder.Property(i => i.CompletedAt);

        builder.Property(i => i.CancelledAt);

        builder.Property(i => i.CancellationReason)
            .HasMaxLength(500);

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Navigation
        builder.HasMany(i => i.Panelists)
            .WithOne(p => p.Interview)
            .HasForeignKey(p => p.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(i => i.Status)
            .HasDatabaseName("ix_final_interviews_status");

        builder.HasIndex(i => i.ScheduledAt)
            .HasDatabaseName("ix_final_interviews_scheduled_at");

        builder.HasIndex(i => i.ScheduledBy)
            .HasDatabaseName("ix_final_interviews_scheduled_by");

        builder.HasIndex(i => i.OverallRecommendation)
            .HasDatabaseName("ix_final_interviews_recommendation");

        // Ignore Entity.Id — we use ApplicationId as PK
        builder.Ignore(i => i.Id);

        // Note: Cross-schema FK to recruitment.applications is added via raw SQL in migration.
        // Note: Cross-schema FK to auth.users (scheduled_by, decided_by) is added via raw SQL in migration.
    }
}
