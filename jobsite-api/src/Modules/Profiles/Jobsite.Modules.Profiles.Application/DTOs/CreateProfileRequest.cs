namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/profiles/me</c>.</summary>
public sealed class CreateProfileRequest
{
    /// <summary>Applicant's first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>Applicant's last name.</summary>
    public required string LastName { get; init; }

    /// <summary>Contact phone number.</summary>
    public string? Phone { get; init; }

    /// <summary>City of residence.</summary>
    public string? City { get; init; }

    /// <summary>Country of residence.</summary>
    public string? Country { get; init; }

    /// <summary>Self-reported skills.</summary>
    public List<SkillDto>? Skills { get; init; }

    /// <summary>Social media links.</summary>
    public SocialLinksDto? SocialLinks { get; init; }

    /// <summary>Uploaded documents (cover letters, certifications).</summary>
    public List<DocumentDto>? Documents { get; init; }
}
