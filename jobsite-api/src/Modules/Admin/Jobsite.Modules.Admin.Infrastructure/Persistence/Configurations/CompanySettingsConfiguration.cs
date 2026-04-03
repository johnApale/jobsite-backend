using Jobsite.Modules.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Admin.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>admin.company_settings</c> table.
/// </summary>
public sealed class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("company_settings", "admin");

        builder.HasKey(cs => cs.Id);
        builder.Property(cs => cs.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(cs => cs.DefaultTimezone)
            .HasMaxLength(50)
            .IsRequired()
            .HasDefaultValue("UTC");

        builder.Property(cs => cs.DefaultCurrency)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue("USD");

        builder.Property(cs => cs.AuthSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.ProfileSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.ScreeningSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.MatchingSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.AssessmentSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.NotificationSettings)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(cs => cs.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(cs => cs.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
