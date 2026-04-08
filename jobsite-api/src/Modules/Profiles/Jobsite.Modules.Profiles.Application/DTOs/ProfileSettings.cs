namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>
/// Deserialization target for tenant profile settings from Admin module's
/// <c>company_settings.profile_settings</c> JSONB column.
/// Read via <see cref="SharedKernel.Persistence.ITenantSettingsReader"/>.
/// </summary>
public sealed class ProfileSettings
{
    public List<string> RequiredProfileFields { get; init; } = ["phone", "skills"];
    public List<string> RequiredSocialLinks { get; init; } = ["linkedin"];
    public List<string> RequiredDocuments { get; init; } = ["CoverLetter"];
    public int MinimumSkillsCount { get; init; } = 3;
    public bool ResumeRequired { get; init; } = true;
}
