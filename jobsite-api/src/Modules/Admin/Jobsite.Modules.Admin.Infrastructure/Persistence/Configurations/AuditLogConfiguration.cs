using Jobsite.Modules.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Admin.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>admin.audit_logs</c> table.
/// Append-only — no FKs, denormalized actor data.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs", "admin");

        builder.HasKey(al => al.Id);
        builder.Property(al => al.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(al => al.ActorId)
            .IsRequired();

        builder.Property(al => al.ActorEmail)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(al => al.ActorRole)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(al => al.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(al => al.EntityType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(al => al.Details)
            .HasColumnType("jsonb");

        builder.Property(al => al.IpAddress)
            .HasMaxLength(45);

        builder.Property(al => al.UserAgent)
            .HasMaxLength(500);

        builder.Property(al => al.PerformedAt)
            .IsRequired();

        builder.Property(al => al.CreatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(al => al.ActorId)
            .HasDatabaseName("ix_audit_logs_actor_id");

        builder.HasIndex(al => al.Action)
            .HasDatabaseName("ix_audit_logs_action");

        builder.HasIndex(al => new { al.EntityType, al.EntityId })
            .HasDatabaseName("ix_audit_logs_entity");

        builder.HasIndex(al => al.PerformedAt)
            .HasDatabaseName("ix_audit_logs_performed_at");
    }
}
