using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>profiles.resumes</c> table.
/// </summary>
public sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("resumes", "profiles", t =>
        {
            t.HasCheckConstraint(
                "chk_resumes_file_type",
                $"file_type IN ('{FileType.Pdf}', '{FileType.Docx}')");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.Property(r => r.FileUrl)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(r => r.OriginalFilename)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.FileSizeBytes)
            .IsRequired();

        builder.Property(r => r.FileType)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(r => r.IsLatest)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.IsParsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ParsedText);

        builder.Property(r => r.ExtractedSkills)
            .HasColumnType("jsonb");

        builder.Property(r => r.AiParsedContent)
            .HasColumnType("jsonb");

        builder.Property(r => r.ParseError)
            .HasMaxLength(2000);

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_resumes_user_id");

        builder.HasIndex(r => r.IsParsed)
            .HasDatabaseName("ix_resumes_is_parsed");

        builder.HasIndex(r => new { r.UserId, r.IsLatest })
            .HasDatabaseName("ix_resumes_user_id_is_latest");
    }
}
