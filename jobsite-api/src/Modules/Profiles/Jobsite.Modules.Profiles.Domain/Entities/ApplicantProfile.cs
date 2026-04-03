using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Profiles.Domain.Entities;

/// <summary>
/// Professional profile for an applicant — maps to <c>profiles.applicant_profiles</c>.
/// Shared PK with <c>auth.users</c>: the <see cref="Entity.Id"/> is the user's ID.
/// One-to-one relationship enforced at the database level via PK = FK.
/// </summary>
public sealed class ApplicantProfile : AggregateRoot
{
    /// <summary>First name (denormalized from auth for profile display).</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>Last name (denormalized from auth for profile display).</summary>
    public string LastName { get; set; } = null!;

    /// <summary>Contact phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>City of residence.</summary>
    public string? City { get; set; }

    /// <summary>Country of residence.</summary>
    public string? Country { get; set; }

    /// <summary>
    /// Self-reported skills as JSONB array.
    /// Format: <c>[{ "name": "C#", "level": "Advanced", "years": 7 }]</c>
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// Social media links as JSONB object.
    /// Format: <c>{ "linkedin": "...", "github": "...", "portfolio": "..." }</c>
    /// </summary>
    public string? SocialLinks { get; set; }

    /// <summary>
    /// Uploaded documents (cover letters, certifications) as JSONB array.
    /// Format: <c>[{ "type": "CoverLetter", "url": "...", "filename": "...", "uploaded_at": "..." }]</c>
    /// </summary>
    public string? Documents { get; set; }

    /// <summary>
    /// Set when the applicant meets the tenant's required profile fields.
    /// Evaluated by the Profiles module using Admin's CompanySettings configuration.
    /// NULL until profile completion requirements are met.
    /// </summary>
    public DateTime? ProfileCompletedAt { get; set; }

    /// <summary>Resumes uploaded by this applicant.</summary>
    public List<Resume> Resumes { get; set; } = [];

    /// <summary>Raise a domain event from this aggregate root.</summary>
    public void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
}
