using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Tenancy.Domain.Entities;

/// <summary>
/// Visual customization for the tenant portal — maps to <c>catalog.tenant_brandings</c>.
/// One-to-one with <see cref="Tenant"/> via shared primary key (<c>tenant_id</c>).
/// Eager-loaded with tenant on resolution and cached in Redis.
/// </summary>
public sealed class TenantBranding : Entity
{
    /// <summary>Shared PK and FK — references <c>catalog.tenants.id</c>.</summary>
    public Guid TenantId { get; set; }

    /// <summary>CDN URL for the company logo.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>CDN URL for the portal favicon.</summary>
    public string? FaviconUrl { get; set; }

    /// <summary>Hex color for buttons, links, accents (e.g., <c>#1A73E8</c>).</summary>
    public string? PrimaryColor { get; set; }

    /// <summary>Hex color for backgrounds, hover states.</summary>
    public string? SecondaryColor { get; set; }

    /// <summary>Displayed on the login/landing page.</summary>
    public string? Tagline { get; set; }

    /// <summary>Navigation property back to the owning tenant.</summary>
    public Tenant Tenant { get; set; } = null!;
}
