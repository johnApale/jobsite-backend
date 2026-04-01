using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jobsite.Modules.Tenancy.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <c>catalog.tenant_brandings</c> table.
/// Shared primary key with <c>catalog.tenants</c> enforces one-to-one.
/// </summary>
public sealed class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_brandings", "catalog");

        // Shared primary key — TenantId is both PK and FK
        builder.HasKey(b => b.TenantId);

        // No Id default — the PK is TenantId, not the inherited Id
        builder.Ignore(b => b.Id);

        builder.Property(b => b.LogoUrl)
            .HasMaxLength(2048);

        builder.Property(b => b.FaviconUrl)
            .HasMaxLength(2048);

        builder.Property(b => b.PrimaryColor)
            .HasMaxLength(9);

        builder.Property(b => b.SecondaryColor)
            .HasMaxLength(9);

        builder.Property(b => b.Tagline)
            .HasMaxLength(500);

        builder.Property(b => b.CreatedAt)
            .HasDefaultValueSql("NOW()");

        builder.Property(b => b.UpdatedAt)
            .HasDefaultValueSql("NOW()");
    }
}
