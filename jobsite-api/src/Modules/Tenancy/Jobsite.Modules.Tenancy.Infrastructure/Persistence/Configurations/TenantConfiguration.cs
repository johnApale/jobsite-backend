using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Tenancy.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>catalog.tenants</c> table.
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", "catalog");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Subdomain)
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(t => t.ConnectionString)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.OwnerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.OwnerEmail)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(t => t.ContactName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.ContactEmail)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(t => t.UpdatedAt)
            .HasDefaultValueSql("NOW()");

        // Indexes
        builder.HasIndex(t => t.Subdomain)
            .IsUnique()
            .HasDatabaseName("ix_tenants_subdomain");

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("ix_tenants_name");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("ix_tenants_status");

        // One-to-one with TenantBranding
        builder.HasOne(t => t.Branding)
            .WithOne(b => b.Tenant)
            .HasForeignKey<TenantBranding>(b => b.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // CHECK constraint for status
        builder.ToTable(t => t.HasCheckConstraint(
            "chk_tenants_status",
            $"status IN ('{TenantStatus.Provisioning}', '{TenantStatus.Active}', '{TenantStatus.Suspended}', '{TenantStatus.Deactivated}')"));

        // Ignore domain events from AggregateRoot
        builder.Ignore(t => t.DomainEvents);
    }
}
