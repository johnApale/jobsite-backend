namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Social media links for the applicant profile.</summary>
public sealed class SocialLinksDto
{
    /// <summary>LinkedIn profile URL.</summary>
    public string? LinkedIn { get; init; }

    /// <summary>GitHub profile URL.</summary>
    public string? GitHub { get; init; }

    /// <summary>Personal portfolio/website URL.</summary>
    public string? Portfolio { get; init; }
}
