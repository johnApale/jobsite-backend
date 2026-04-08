using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Admin.Application.Services;

/// <summary>
/// Aggregates pipeline statistics from Recruitment, Screening, and Matching modules
/// via cross-module readers defined in SharedKernel.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IRecruitmentStatsReader _recruitmentStatsReader;
    private readonly IScreeningStatsReader _screeningStatsReader;
    private readonly IMatchingStatsReader _matchingStatsReader;

    public DashboardService(
        IRecruitmentStatsReader recruitmentStatsReader,
        IScreeningStatsReader screeningStatsReader,
        IMatchingStatsReader matchingStatsReader)
    {
        _recruitmentStatsReader = recruitmentStatsReader;
        _screeningStatsReader = screeningStatsReader;
        _matchingStatsReader = matchingStatsReader;
    }

    public async Task<DashboardStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        RecruitmentStatsSnapshot recruitmentStats = await _recruitmentStatsReader.GetStatsAsync(ct);
        ScreeningStatsSnapshot screeningStats = await _screeningStatsReader.GetStatsAsync(ct);
        MatchingStatsSnapshot matchingStats = await _matchingStatsReader.GetStatsAsync(ct);

        return new DashboardStatsResponse
        {
            Recruitment = new RecruitmentStats
            {
                TotalJobPostings = recruitmentStats.TotalJobPostings,
                ActiveJobPostings = recruitmentStats.ActiveJobPostings,
                ClosedJobPostings = recruitmentStats.ClosedJobPostings,
                TotalApplications = recruitmentStats.TotalApplications,
                SubmittedApplications = recruitmentStats.SubmittedApplications,
                ScreeningApplications = recruitmentStats.ScreeningApplications,
                ShortlistedApplications = recruitmentStats.ShortlistedApplications,
                RejectedApplications = recruitmentStats.RejectedApplications,
                HiredApplications = recruitmentStats.HiredApplications,
                WithdrawnApplications = recruitmentStats.WithdrawnApplications
            },
            Screening = new ScreeningStats
            {
                TotalScreenings = screeningStats.TotalScreenings,
                CompletedScreenings = screeningStats.CompletedScreenings,
                PendingScreenings = screeningStats.PendingScreenings,
                FailedScreenings = screeningStats.FailedScreenings,
                AverageScore = screeningStats.AverageScore,
                AutoAdvancedCount = screeningStats.AutoAdvancedCount,
                AutoRejectedCount = screeningStats.AutoRejectedCount,
                ManualReviewCount = screeningStats.ManualReviewCount
            },
            Matching = new MatchingStats
            {
                TotalShortlists = matchingStats.TotalShortlists,
                DraftShortlists = matchingStats.DraftShortlists,
                FinalizedShortlists = matchingStats.FinalizedShortlists,
                TotalCandidateMatches = matchingStats.TotalCandidateMatches
            }
        };
    }
}
