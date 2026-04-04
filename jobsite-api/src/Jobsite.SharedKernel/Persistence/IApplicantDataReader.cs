namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads applicant profile and resume data for screening.
/// Implemented by Profiles.Infrastructure to avoid cross-module project references.
/// Consumed by the Screening module during candidate evaluation.
/// </summary>
public interface IApplicantDataReader
{
    /// <summary>
    /// Returns the applicant's profile skills and parsed resume content
    /// for use in screening evaluation.
    /// </summary>
    Task<ApplicantDataSnapshot?> GetApplicantDataAsync(
        Guid applicantUserId, Guid? resumeId, CancellationToken ct = default);
}

/// <summary>
/// Snapshot of applicant data needed for screening evaluation —
/// profile skills plus parsed resume content.
/// </summary>
public sealed class ApplicantDataSnapshot
{
    public required Guid UserId { get; init; }

    /// <summary>JSONB skills array from the applicant's profile.</summary>
    public string? ProfileSkills { get; init; }

    /// <summary>Plain text extracted from the resume (basic parser).</summary>
    public string? ResumeParsedText { get; init; }

    /// <summary>Skills extracted from the resume by the basic parser.</summary>
    public string? ResumeExtractedSkills { get; init; }

    /// <summary>
    /// AI-parsed structured content from the resume (JSONB).
    /// Contains skills with levels/years, experience timeline, education, certifications.
    /// Preferred over basic parsed data when available.
    /// </summary>
    public string? AiParsedContent { get; init; }
}
