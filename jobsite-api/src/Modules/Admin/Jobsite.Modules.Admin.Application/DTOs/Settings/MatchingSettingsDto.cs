namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Matching configuration settings (JSONB shape for <c>company_settings.matching_settings</c>).</summary>
public sealed class MatchingSettingsDto
{
    public int ScreeningWeight { get; set; } = 100;
    public int AssessmentWeight { get; set; }
    public bool AutoGenerateShortlist { get; set; } = true;
    public int ShortlistSize { get; set; } = 10;
}
