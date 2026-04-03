using Jobsite.Modules.Admin.Application.DTOs.Settings;

namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>Response body for <c>GET /api/v1/admin/settings</c>.</summary>
public sealed class CompanySettingsResponse
{
    public required Guid Id { get; init; }
    public required string DefaultTimezone { get; init; }
    public required string DefaultCurrency { get; init; }
    public required AuthSettingsDto AuthSettings { get; init; }
    public required ProfileSettingsDto ProfileSettings { get; init; }
    public required ScreeningSettingsDto ScreeningSettings { get; init; }
    public required MatchingSettingsDto MatchingSettings { get; init; }
    public required AssessmentSettingsDto AssessmentSettings { get; init; }
    public required NotificationSettingsDto NotificationSettings { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
