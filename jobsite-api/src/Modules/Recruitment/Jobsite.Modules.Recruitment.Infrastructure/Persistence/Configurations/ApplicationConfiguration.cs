using Jobsite.Modules.Recruitment.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>recruitment.applications</c> table.
/// Cross-schema FKs to <c>auth.users</c> (applicant_id) and <c>profiles.resumes</c> (resume_id)
/// added via raw SQL in migration.
/// </summary>
public sealed class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.ToTable("applications", "recruitment", t =>
        {
            t.HasCheckConstraint(
                "chk_applications_status",
                $"status IN ('{ApplicationStatus.Submitted}', '{ApplicationStatus.Screening}', '{ApplicationStatus.Assessment}', '{ApplicationStatus.Shortlisted}', '{ApplicationStatus.FinalInterview}', '{ApplicationStatus.Offered}', '{ApplicationStatus.Hired}', '{ApplicationStatus.Rejected}', '{ApplicationStatus.Withdrawn}')");

            t.HasCheckConstraint(
                "chk_applications_rejected_at_stage",
                $"rejected_at_stage IS NULL OR rejected_at_stage IN ('{RejectedAtStage.Screening}', '{RejectedAtStage.Assessment}', '{RejectedAtStage.Shortlisted}', '{RejectedAtStage.FinalInterview}', '{RejectedAtStage.Offered}')");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.JobPostingId)
            .IsRequired();

        builder.Property(a => a.ApplicantId)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ResumeId)
            .IsRequired();

        builder.Property(a => a.CoverLetterUrl)
            .HasMaxLength(2048);

        builder.Property(a => a.RejectionReason)
            .HasMaxLength(500);

        builder.Property(a => a.RejectedAtStage)
            .HasMaxLength(20);

        builder.Property(a => a.SubmittedAt)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(a => a.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Unique constraint: one application per person per job
        builder.HasIndex(a => new { a.ApplicantId, a.JobPostingId })
            .IsUnique()
            .HasDatabaseName("uq_applications_applicant_job");

        // Indexes
        builder.HasIndex(a => a.JobPostingId)
            .HasDatabaseName("ix_applications_job_posting_id");

        builder.HasIndex(a => a.ApplicantId)
            .HasDatabaseName("ix_applications_applicant_id");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("ix_applications_status");

        builder.HasIndex(a => a.SubmittedAt)
            .HasDatabaseName("ix_applications_submitted_at");

        // Ignore domain events from AggregateRoot
        builder.Ignore(a => a.DomainEvents);
    }
}
