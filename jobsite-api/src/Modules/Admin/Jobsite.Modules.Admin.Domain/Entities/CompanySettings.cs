using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Admin.Domain.Entities;

/// <summary>
/// Tenant-scoped configuration singleton — one row per tenant database.
/// Controls all configurable behavior across the platform: auth, profiles,
/// screening thresholds, matching weights, assessment, and notifications.
/// Maps to <c>admin.company_settings</c>.
/// </summary>
public sealed class CompanySettings : Entity
{
    /// <summary>IANA timezone (e.g., America/New_York). Default UTC.</summary>
    public string DefaultTimezone { get; set; } = "UTC";

    /// <summary>ISO 4217 currency code. Default USD.</summary>
    public string DefaultCurrency { get; set; } = "USD";

    /// <summary>OAuth provider toggles and auth configuration (JSONB).</summary>
    public string AuthSettings { get; set; } = null!;

    /// <summary>Required profile fields, social links, documents (JSONB).</summary>
    public string ProfileSettings { get; set; } = null!;

    /// <summary>Thresholds, review policy, AI scoring, transparency, default criteria (JSONB).</summary>
    public string ScreeningSettings { get; set; } = null!;

    /// <summary>Screening/assessment weight split (JSONB).</summary>
    public string MatchingSettings { get; set; } = null!;

    /// <summary>Assessment phase configuration, completion policy (JSONB).</summary>
    public string AssessmentSettings { get; set; } = null!;

    /// <summary>Email notification preferences (JSONB).</summary>
    public string NotificationSettings { get; set; } = null!;
}
