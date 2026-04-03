namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>
/// Request body for <c>PATCH /api/v1/profiles/me</c>.
/// All fields are nullable — only non-null values are applied (JSON merge patch semantics).
/// </summary>
public sealed class UpdateProfileRequest
{
    /// <summary>Applicant's first name.</summary>
    public string? FirstName { get; init; }

    /// <summary>Applicant's last name.</summary>
    public string? LastName { get; init; }

    /// <summary>Contact phone number.</summary>
    public string? Phone { get; init; }

    /// <summary>City of residence.</summary>
    public string? City { get; init; }

    /// <summary>Country of residence.</summary>
    public string? Country { get; init; }

    /// <summary>Self-reported skills (replaces entire list when provided).</summary>
    public List<SkillDto>? Skills { get; init; }

    /// <summary>Social media links (replaces entire object when provided).</summary>
    public SocialLinksDto? SocialLinks { get; init; }

    /// <summary>Uploaded documents (replaces entire list when provided).</summary>
    public List<DocumentDto>? Documents { get; init; }
}
