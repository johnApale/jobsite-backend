namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Profile configuration settings (JSONB shape for <c>company_settings.profile_settings</c>).</summary>
public sealed class ProfileSettingsDto
{
    public List<string> RequiredProfileFields { get; set; } = ["phone", "skills"];
    public List<string> RequiredSocialLinks { get; set; } = ["linkedin"];
    public List<string> RequiredDocuments { get; set; } = ["CoverLetter"];
    public int MinimumSkillsCount { get; set; } = 3;
    public bool ResumeRequired { get; set; } = true;
    public bool AiParsingEnabled { get; set; } = true;
    public string AiParsingProvider { get; set; } = "OpenAI";
}
