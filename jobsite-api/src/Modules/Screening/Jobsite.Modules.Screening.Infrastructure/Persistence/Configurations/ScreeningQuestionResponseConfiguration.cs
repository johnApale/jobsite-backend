using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence.Configurations;

public sealed class ScreeningQuestionResponseConfiguration : IEntityTypeConfiguration<ScreeningQuestionResponse>
{
    public void Configure(EntityTypeBuilder<ScreeningQuestionResponse> builder)
    {
        builder.ToTable("screening_question_responses", "screening", t =>
        {
            t.HasCheckConstraint("chk_question_responses_score_result",
                $"score_result IS NULL OR score_result IN ('{ScoreResult.MeetsRequirement}', '{ScoreResult.PartialMatch}', '{ScoreResult.Missing}')");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.ApplicationId)
            .IsRequired();

        builder.Property(r => r.QuestionId)
            .IsRequired();

        builder.Property(r => r.ResponseText)
            .HasColumnType("text");

        builder.Property(r => r.ResponseData)
            .HasColumnType("jsonb");

        builder.Property(r => r.Score)
            .HasColumnType("decimal(5,2)");

        builder.Property(r => r.ScoreResult)
            .HasMaxLength(20);

        builder.Property(r => r.ScoreReasoning)
            .HasColumnType("text");

        builder.Property(r => r.SubmittedAt)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Unique constraint: one answer per question per application
        builder.HasIndex(r => new { r.ApplicationId, r.QuestionId })
            .IsUnique()
            .HasDatabaseName("uq_question_responses_app_question");

        // Index for querying all answers for an application
        builder.HasIndex(r => r.ApplicationId)
            .HasDatabaseName("ix_question_responses_application_id");

        // Note: Cross-schema FK to recruitment.applications is added via raw SQL in migration.
        // Note: Cross-schema FK to recruitment.job_screening_questions is added via raw SQL in migration.
    }
}
