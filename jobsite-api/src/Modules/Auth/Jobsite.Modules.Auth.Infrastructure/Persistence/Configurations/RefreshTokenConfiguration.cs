using Jobsite.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>auth.refresh_tokens</c> table.
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "auth");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.TokenHash)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.FamilyId)
            .IsRequired();

        builder.Property(r => r.IsRevoked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(r => r.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(r => r.TokenHash)
            .IsUnique()
            .HasDatabaseName("ix_refresh_tokens_token_hash");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("ix_refresh_tokens_user_id");

        builder.HasIndex(r => r.FamilyId)
            .HasDatabaseName("ix_refresh_tokens_family_id");

        builder.HasIndex(r => r.ExpiresAt)
            .HasDatabaseName("ix_refresh_tokens_expires_at");

        // Self-referencing FK for token chain
        builder.HasOne<RefreshToken>()
            .WithMany()
            .HasForeignKey(r => r.ReplacedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Ignore computed property
        builder.Ignore(r => r.IsExpired);
    }
}
