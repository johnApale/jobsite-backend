using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>auth.users</c> table.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "auth");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(200);

        builder.Property(u => u.EmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.Role)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(2048);

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(u => u.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.Role)
            .HasDatabaseName("ix_users_role");

        builder.HasIndex(u => u.Status)
            .HasDatabaseName("ix_users_status");

        builder.HasIndex(u => u.InvitedBy)
            .HasDatabaseName("ix_users_invited_by");

        // Self-referencing FK for invited_by
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(u => u.InvitedBy)
            .OnDelete(DeleteBehavior.SetNull);

        // One-to-many with UserExternalLogin
        builder.HasMany(u => u.ExternalLogins)
            .WithOne(e => e.User)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many with RefreshToken
        builder.HasMany(u => u.RefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // CHECK constraints
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_users_role",
            $"role IN ('{UserRole.Applicant}', '{UserRole.Recruiter}', '{UserRole.HiringManager}', '{UserRole.Interviewer}', '{UserRole.AgencyAdmin}')"));

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_users_status",
            $"status IN ('{UserStatus.Active}', '{UserStatus.Invited}', '{UserStatus.Deactivated}')"));

        // Ignore domain events from AggregateRoot
        builder.Ignore(u => u.DomainEvents);
    }
}
