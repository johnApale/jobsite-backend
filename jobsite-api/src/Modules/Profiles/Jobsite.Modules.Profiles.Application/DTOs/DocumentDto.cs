namespace Jobsite.Modules.Profiles.Application.DTOs;

/// <summary>Document entry in the applicant profile's documents list.</summary>
public sealed class DocumentDto
{
    /// <summary>Document type (e.g. "CoverLetter", "Certification").</summary>
    public required string Type { get; init; }

    /// <summary>Storage URL for the document.</summary>
    public required string Url { get; init; }

    /// <summary>Original filename.</summary>
    public required string Filename { get; init; }

    /// <summary>When the document was uploaded.</summary>
    public required DateTime UploadedAt { get; init; }
}
