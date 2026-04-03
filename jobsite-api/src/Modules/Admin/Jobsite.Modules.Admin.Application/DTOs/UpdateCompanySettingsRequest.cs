using Jobsite.Modules.Admin.Application.DTOs.Settings;

namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>
/// Request body for <c>PATCH /api/v1/admin/settings</c>.
/// All fields are nullable — only non-null values are applied (JSON merge patch semantics).
/// </summary>
public sealed class UpdateCompanySettingsRequest
{
    public string? DefaultTimezone { get; init; }
    public string? DefaultCurrency { get; init; }
    public AuthSettingsDto? AuthSettings { get; init; }
    public ProfileSettingsDto? ProfileSettings { get; init; }
    public ScreeningSettingsDto? ScreeningSettings { get; init; }
    public MatchingSettingsDto? MatchingSettings { get; init; }
    public AssessmentSettingsDto? AssessmentSettings { get; init; }
    public NotificationSettingsDto? NotificationSettings { get; init; }
}
