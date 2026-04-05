namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads basic application data (job posting, applicant) from the Recruitment module.
/// Defined in SharedKernel; implemented by Recruitment.Infrastructure.
/// Consumed by the Matching module when handling screening-completed events.
/// </summary>
public interface IApplicationDataReader
{
    /// <summary>
    /// Returns the job posting ID and applicant user ID for an application,
    /// or <c>null</c> if the application does not exist.
    /// </summary>
    Task<ApplicationDataSnapshot?> GetApplicationDataAsync(Guid applicationId, CancellationToken ct = default);
}

/// <summary>Projection of application data needed by the Matching module.</summary>
public sealed class ApplicationDataSnapshot
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantUserId { get; init; }
}
