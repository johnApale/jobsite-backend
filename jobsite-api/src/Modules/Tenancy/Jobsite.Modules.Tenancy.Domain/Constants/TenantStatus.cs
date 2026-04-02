namespace Jobsite.Modules.Tenancy.Domain.Constants;

/// <summary>
/// Lifecycle status constants for the <c>catalog.tenants.status</c> column.
/// Values must match the CHECK constraint <c>chk_tenants_status</c> exactly.
/// </summary>
public static class TenantStatus
{
    public const string Provisioning = "Provisioning";
    public const string ProvisioningFailed = "ProvisioningFailed";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
    public const string Deactivated = "Deactivated";

    public static bool IsValid(string status) =>
        status is Provisioning or ProvisioningFailed or Active or Suspended or Deactivated;
}
