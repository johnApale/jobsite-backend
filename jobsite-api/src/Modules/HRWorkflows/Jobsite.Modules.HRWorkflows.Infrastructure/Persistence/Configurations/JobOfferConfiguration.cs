using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Configurations;

public sealed class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        builder.ToTable("job_offers", "hr_workflows", t =>
        {
            t.HasCheckConstraint("chk_job_offers_status",
                $"status IN ('{OfferStatus.Draft}', '{OfferStatus.Pending}', '{OfferStatus.Accepted}', '{OfferStatus.Declined}', '{OfferStatus.Withdrawn}', '{OfferStatus.Expired}')");

            t.HasCheckConstraint("chk_job_offers_salary_period",
                $"salary_period IN ('{SalaryPeriod.Annual}', '{SalaryPeriod.Monthly}', '{SalaryPeriod.Hourly}')");

            t.HasCheckConstraint("chk_job_offers_employment_type",
                $"employment_type IN ('{OfferEmploymentType.FullTime}', '{OfferEmploymentType.PartTime}', '{OfferEmploymentType.Contract}', '{OfferEmploymentType.Temporary}')");
        });

        // Shared PK with recruitment.applications
        builder.HasKey(o => o.ApplicationId);
        builder.Property(o => o.ApplicationId)
            .ValueGeneratedNever();

        builder.Property(o => o.ClientCompanyId);

        builder.Property(o => o.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Salary)
            .HasColumnType("decimal(12,2)")
            .IsRequired();

        builder.Property(o => o.SalaryCurrency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(o => o.SalaryPeriod)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.EmploymentType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.StartDate);

        builder.Property(o => o.Benefits);

        builder.Property(o => o.AdditionalTerms);

        builder.Property(o => o.OfferLetterUrl)
            .HasMaxLength(2048);

        builder.Property(o => o.ExpiresAt);

        builder.Property(o => o.ExtendedBy)
            .IsRequired();

        builder.Property(o => o.ExtendedAt);

        builder.Property(o => o.RespondedAt);

        builder.Property(o => o.DeclineReason)
            .HasMaxLength(500);

        builder.Property(o => o.WithdrawnAt);

        builder.Property(o => o.WithdrawalReason)
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasDefaultValueSql("NOW()")
            .IsRequired();

        // Indexes
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("ix_job_offers_status");

        builder.HasIndex(o => o.ExtendedBy)
            .HasDatabaseName("ix_job_offers_extended_by");

        builder.HasIndex(o => o.ExpiresAt)
            .HasDatabaseName("ix_job_offers_expires_at");

        // Ignore Entity.Id — we use ApplicationId as PK
        builder.Ignore(o => o.Id);

    }
}
