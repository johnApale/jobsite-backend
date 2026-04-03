using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>recruitment.job_evaluation_criteria</c> table.
/// </summary>
public sealed class JobEvaluationCriteriaConfiguration : IEntityTypeConfiguration<JobEvaluationCriteria>
{
    public void Configure(EntityTypeBuilder<JobEvaluationCriteria> builder)
    {
        builder.ToTable("job_evaluation_criteria", "recruitment", t =>
        {
            t.HasCheckConstraint(
                "chk_criteria_category",
                $"category IN ('{CriteriaCategory.Skill}', '{CriteriaCategory.Experience}', '{CriteriaCategory.Certification}', '{CriteriaCategory.Education}', '{CriteriaCategory.Location}', '{CriteriaCategory.Custom}')");

            t.HasCheckConstraint(
                "chk_criteria_evaluation_method",
                $"evaluation_method IN ('{EvaluationMethod.ExactMatch}', '{EvaluationMethod.RangeMatch}', '{EvaluationMethod.SemanticSimilarity}')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.JobPostingId)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Category)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.EvaluationMethod)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(c => c.IsRequired)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(c => c.Weight)
            .HasColumnType("decimal(5,2)")
            .IsRequired();

        builder.Property(c => c.Configuration)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.DisplayOrder)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(c => c.JobPostingId)
            .HasDatabaseName("ix_criteria_job_posting_id");

        builder.HasIndex(c => c.Category)
            .HasDatabaseName("ix_criteria_category");
    }
}
