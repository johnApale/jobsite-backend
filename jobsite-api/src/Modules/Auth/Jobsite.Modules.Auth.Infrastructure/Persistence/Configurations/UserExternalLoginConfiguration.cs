using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>auth.user_external_logins</c> table.
/// </summary>
public sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> builder)
    {
        builder.ToTable("user_external_logins", "auth");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.Provider)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.ProviderSubjectId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.ProviderEmail)
            .HasMaxLength(254);

        builder.Property(e => e.ProviderDisplayName)
            .HasMaxLength(200);

        builder.Property(e => e.LinkedAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Unique constraints
        builder.HasIndex(e => new { e.Provider, e.ProviderSubjectId })
            .IsUnique()
            .HasDatabaseName("uq_external_logins_provider_subject");

        builder.HasIndex(e => new { e.UserId, e.Provider })
            .IsUnique()
            .HasDatabaseName("uq_external_logins_user_provider");

        // Lookup index
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_external_logins_user_id");

        // CHECK constraint for provider
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_external_logins_provider",
            $"provider IN ('{ExternalLoginProvider.Google}', '{ExternalLoginProvider.Apple}', '{ExternalLoginProvider.Facebook}')"));
    }
}
