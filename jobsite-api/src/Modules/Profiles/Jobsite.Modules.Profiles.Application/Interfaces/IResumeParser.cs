namespace Jobsite.Modules.Profiles.Application.Interfaces;

/// <summary>
/// Result of parsing a resume file.
/// </summary>
public sealed class ResumeParseResult
{
    /// <summary>Extracted plain text from the resume.</summary>
    public required string ParsedText { get; init; }

    /// <summary>Skills extracted from the resume as JSON array.</summary>
    public string? ExtractedSkills { get; init; }
}

/// <summary>
/// Abstraction for resume file text extraction and skill parsing.
/// </summary>
public interface IResumeParser
{
    /// <summary>Parse a resume file and extract text + skills.</summary>
    Task<ResumeParseResult> ParseAsync(
        string fileUrl, string fileType, CancellationToken ct = default);
}
