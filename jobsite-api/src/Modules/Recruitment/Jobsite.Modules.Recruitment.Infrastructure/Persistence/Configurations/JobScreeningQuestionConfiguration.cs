using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>recruitment.job_screening_questions</c> table.
/// </summary>
public sealed class JobScreeningQuestionConfiguration : IEntityTypeConfiguration<JobScreeningQuestion>
{
    public void Configure(EntityTypeBuilder<JobScreeningQuestion> builder)
    {
        builder.ToTable("job_screening_questions", "recruitment", t =>
        {
            t.HasCheckConstraint(
                "chk_questions_question_type",
                $"question_type IN ('{QuestionType.FreeText}', '{QuestionType.MultipleChoice}', '{QuestionType.YesNo}')");

            t.HasCheckConstraint(
                "chk_questions_timing",
                $"timing IN ('{QuestionTiming.AtApplication}', '{QuestionTiming.AfterScreening}')");
        });

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(q => q.JobPostingId)
            .IsRequired();

        builder.Property(q => q.QuestionText)
            .IsRequired();

        builder.Property(q => q.QuestionType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.Timing)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(q => q.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(q => q.Weight)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(q => q.ExpectedAnswer)
            .HasColumnType("jsonb");

        builder.Property(q => q.Options)
            .HasColumnType("jsonb");

        builder.Property(q => q.DisplayOrder)
            .IsRequired();

        builder.Property(q => q.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(q => q.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(q => q.JobPostingId)
            .HasDatabaseName("ix_questions_job_posting_id");

        builder.HasIndex(q => q.Timing)
            .HasDatabaseName("ix_questions_timing");
    }
}
