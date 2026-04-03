using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Profiles.Domain.Entities;

/// <summary>
/// Uploaded resume with parsing state — maps to <c>profiles.resumes</c>.
/// Supports versioning via <see cref="IsLatest"/> flag (one latest per user).
/// Parsed once on upload; pre-parsed data reused across all applications.
/// </summary>
public sealed class Resume : Entity
{
    /// <summary>The applicant who uploaded this resume.</summary>
    public Guid UserId { get; set; }

    /// <summary>Storage URL (local filesystem or cloud blob).</summary>
    public string FileUrl { get; set; } = null!;

    /// <summary>Original filename from the upload.</summary>
    public string OriginalFilename { get; set; } = null!;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>File type: PDF or DOCX. Constrained by CHECK.</summary>
    public string FileType { get; set; } = null!;

    /// <summary>True if this is the most recently uploaded resume for the user.</summary>
    public bool IsLatest { get; set; }

    /// <summary>True after the background parser has successfully processed this resume.</summary>
    public bool IsParsed { get; set; }

    /// <summary>Full extracted text content. NULL until parsed.</summary>
    public string? ParsedText { get; set; }

    /// <summary>
    /// Skills extracted by the parser as JSONB array.
    /// Format: <c>[{ "name": ".NET", "years": 5, "confidence": 0.95 }]</c>
    /// NULL until parsed.
    /// </summary>
    public string? ExtractedSkills { get; set; }

    /// <summary>
    /// AI-powered structured extraction as JSONB.
    /// Contains skills with levels/years, experience timeline, education, certifications.
    /// NULL if AI Service is unavailable or parsing hasn't run.
    /// </summary>
    public string? AiParsedContent { get; set; }

    /// <summary>Error message if parsing failed. NULL on success or before parsing.</summary>
    public string? ParseError { get; set; }

    /// <summary>Timestamp when parsing completed. NULL until parsed.</summary>
    public DateTime? ParsedAt { get; set; }

    /// <summary>Navigation property back to the owning profile.</summary>
    public ApplicantProfile ApplicantProfile { get; set; } = null!;
}
