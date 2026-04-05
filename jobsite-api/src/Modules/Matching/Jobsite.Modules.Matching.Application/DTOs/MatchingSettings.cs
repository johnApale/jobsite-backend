namespace Jobsite.Modules.Matching.Application.DTOs;

/// <summary>
/// Tenant-scoped matching configuration read from <c>admin.company_settings.matching_settings</c> JSONB.
/// </summary>
public sealed class MatchingSettings
{
    public decimal ScreeningWeight { get; init; } = 60m;
    public decimal AssessmentWeight { get; init; } = 40m;
    public bool AutoGenerateShortlist { get; init; } = true;
    public int ShortlistSize { get; init; } = 10;
}
