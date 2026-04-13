using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Tenancy.Domain.Entities;

/// <summary>
/// Core tenant identity — maps to the <c>catalog.tenants</c> table.
/// The hottest record in the system: resolved on every inbound request via subdomain → Redis cache.
/// </summary>
public sealed class Tenant : AggregateRoot
{
    /// <summary>Company display name. Unique across the platform.</summary>
    public string Name { get; set; } = null!;

    /// <summary>DNS label for <c>{subdomain}.jobsite.com</c>. Unique, max 63 chars.</summary>
    public string Subdomain { get; set; } = null!;

    /// <summary>Routes requests to this tenant's isolated PostgreSQL database.</summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>Lifecycle status: Provisioning, Active, Suspended, Deactivated.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Person who registered the tenant; seeded as initial AgencyAdmin.</summary>
    public string OwnerName { get; set; } = null!;

    /// <summary>Used to seed the first user account in the tenant DB.</summary>
    public string OwnerEmail { get; set; } = null!;

    /// <summary>Receives platform notifications (may differ from owner).</summary>
    public string ContactName { get; set; } = null!;

    /// <summary>Platform-level communications: incidents, provisioning updates.</summary>
    public string ContactEmail { get; set; } = null!;

    /// <summary>Set when the tenant DB is created and migrations complete.</summary>
    public DateTime? ProvisionedAt { get; set; }

    /// <summary>Set when status moves to Deactivated.</summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>Optional branding customization. Null means platform defaults.</summary>
    public TenantBranding? Branding { get; set; }
}
