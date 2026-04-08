namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>Response for <c>GET /api/v1/admin/dashboard</c> — aggregate pipeline statistics.</summary>
public sealed class DashboardStatsResponse
{
    public required RecruitmentStats Recruitment { get; init; }
    public required ScreeningStats Screening { get; init; }
    public required MatchingStats Matching { get; init; }
}

/// <summary>Recruitment pipeline statistics.</summary>
public sealed class RecruitmentStats
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

/// <summary>Screening pipeline statistics.</summary>
public sealed class ScreeningStats
{
    public required int TotalScreenings { get; init; }
    public required int CompletedScreenings { get; init; }
    public required int PendingScreenings { get; init; }
    public required int FailedScreenings { get; init; }
    public required decimal? AverageScore { get; init; }
    public required int AutoAdvancedCount { get; init; }
    public required int AutoRejectedCount { get; init; }
    public required int ManualReviewCount { get; init; }
}

/// <summary>Matching pipeline statistics.</summary>
public sealed class MatchingStats
{
    public required int TotalShortlists { get; init; }
    public required int DraftShortlists { get; init; }
    public required int FinalizedShortlists { get; init; }
    public required int TotalCandidateMatches { get; init; }
}
