namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Response body for profile endpoints.</summary>
public sealed class ProfileResponse
{
    /// <summary>User ID (shared PK with auth.users).</summary>
    public required Guid UserId { get; init; }

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

    /// <summary>Uploaded documents.</summary>
    public List<DocumentDto>? Documents { get; init; }

    /// <summary>When the profile met the tenant's completion requirements. NULL if incomplete.</summary>
    public DateTime? ProfileCompletedAt { get; init; }

    /// <summary>Profile creation timestamp.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Last modification timestamp.</summary>
    public required DateTime UpdatedAt { get; init; }
}
