namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads aggregate recruitment statistics from the Recruitment module.
/// Defined in SharedKernel; implemented by Recruitment.Infrastructure.
/// Consumed by the Admin module for the dashboard endpoint.
/// </summary>
public interface IRecruitmentStatsReader
{
    /// <summary>Returns aggregate job posting and application counts for the current tenant.</summary>
    Task<RecruitmentStatsSnapshot> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>Projection of recruitment statistics needed by the Admin dashboard.</summary>
public sealed class RecruitmentStatsSnapshot
{
    public required int TotalJobPostings { get; init; }
    public required int ActiveJobPostings { get; init; }
    public required int ClosedJobPostings { get; init; }
    public required int TotalApplications { get; init; }
    public required int SubmittedApplications { get; init; }
    public required int ScreeningApplications { get; init; }
    public required int ShortlistedApplications { get; init; }
    public required int RejectedApplications { get; init; }
    public required int HiredApplications { get; init; }
    public required int WithdrawnApplications { get; init; }
}
