using FluentAssertions;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Services;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Admin;

/// <summary>Tests for DashboardService application service.</summary>
public sealed class DashboardServiceTests
{
    private readonly IRecruitmentStatsReader _recruitmentStatsReader;
    private readonly IScreeningStatsReader _screeningStatsReader;
    private readonly IMatchingStatsReader _matchingStatsReader;
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _recruitmentStatsReader = Substitute.For<IRecruitmentStatsReader>();
        _screeningStatsReader = Substitute.For<IScreeningStatsReader>();
        _matchingStatsReader = Substitute.For<IMatchingStatsReader>();
        _sut = new DashboardService(_recruitmentStatsReader, _screeningStatsReader, _matchingStatsReader);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsAggregatedStats()
    {
        // Arrange
        RecruitmentStatsSnapshot recruitmentStats = new()
        {
            TotalJobPostings = 10,
            ActiveJobPostings = 5,
            ClosedJobPostings = 3,
            TotalApplications = 50,
            SubmittedApplications = 20,
            ScreeningApplications = 10,
            ShortlistedApplications = 8,
            RejectedApplications = 5,
            HiredApplications = 2,
            WithdrawnApplications = 5
        };

        ScreeningStatsSnapshot screeningStats = new()
        {
            TotalScreenings = 30,
            CompletedScreenings = 25,
            PendingScreenings = 3,
            FailedScreenings = 2,
            AverageScore = 72.5m,
            AutoAdvancedCount = 15,
            AutoRejectedCount = 5,
            ManualReviewCount = 5
        };

        MatchingStatsSnapshot matchingStats = new()
        {
            TotalShortlists = 4,
            DraftShortlists = 2,
            FinalizedShortlists = 2,
            TotalCandidateMatches = 20
        };

        _recruitmentStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(recruitmentStats);
        _screeningStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(screeningStats);
        _matchingStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(matchingStats);

        // Act
        DashboardStatsResponse result = await _sut.GetStatsAsync(CancellationToken.None);

        // Assert
        result.Recruitment.TotalJobPostings.Should().Be(10);
        result.Recruitment.ActiveJobPostings.Should().Be(5);
        result.Recruitment.ClosedJobPostings.Should().Be(3);
        result.Recruitment.TotalApplications.Should().Be(50);
        result.Recruitment.SubmittedApplications.Should().Be(20);
        result.Recruitment.ScreeningApplications.Should().Be(10);
        result.Recruitment.ShortlistedApplications.Should().Be(8);
        result.Recruitment.RejectedApplications.Should().Be(5);
        result.Recruitment.HiredApplications.Should().Be(2);
        result.Recruitment.WithdrawnApplications.Should().Be(5);

        result.Screening.TotalScreenings.Should().Be(30);
        result.Screening.CompletedScreenings.Should().Be(25);
        result.Screening.PendingScreenings.Should().Be(3);
        result.Screening.FailedScreenings.Should().Be(2);
        result.Screening.AverageScore.Should().Be(72.5m);
        result.Screening.AutoAdvancedCount.Should().Be(15);
        result.Screening.AutoRejectedCount.Should().Be(5);
        result.Screening.ManualReviewCount.Should().Be(5);

        result.Matching.TotalShortlists.Should().Be(4);
        result.Matching.DraftShortlists.Should().Be(2);
        result.Matching.FinalizedShortlists.Should().Be(2);
        result.Matching.TotalCandidateMatches.Should().Be(20);
    }

    [Fact]
    public async Task GetStatsAsync_WithNullAverageScore_ReturnsNullScore()
    {
        // Arrange
        _recruitmentStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new RecruitmentStatsSnapshot
        {
            TotalJobPostings = 0,
            ActiveJobPostings = 0,
            ClosedJobPostings = 0,
            TotalApplications = 0,
            SubmittedApplications = 0,
            ScreeningApplications = 0,
            ShortlistedApplications = 0,
            RejectedApplications = 0,
            HiredApplications = 0,
            WithdrawnApplications = 0
        });

        _screeningStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new ScreeningStatsSnapshot
        {
            TotalScreenings = 0,
            CompletedScreenings = 0,
            PendingScreenings = 0,
            FailedScreenings = 0,
            AverageScore = null,
            AutoAdvancedCount = 0,
            AutoRejectedCount = 0,
            ManualReviewCount = 0
        });

        _matchingStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new MatchingStatsSnapshot
        {
            TotalShortlists = 0,
            DraftShortlists = 0,
            FinalizedShortlists = 0,
            TotalCandidateMatches = 0
        });

        // Act
        DashboardStatsResponse result = await _sut.GetStatsAsync(CancellationToken.None);

        // Assert
        result.Screening.AverageScore.Should().BeNull();
        result.Recruitment.TotalJobPostings.Should().Be(0);
        result.Matching.TotalShortlists.Should().Be(0);
    }

    [Fact]
    public async Task GetStatsAsync_CallsAllReaders()
    {
        // Arrange
        _recruitmentStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new RecruitmentStatsSnapshot
        {
            TotalJobPostings = 0,
            ActiveJobPostings = 0,
            ClosedJobPostings = 0,
            TotalApplications = 0,
            SubmittedApplications = 0,
            ScreeningApplications = 0,
            ShortlistedApplications = 0,
            RejectedApplications = 0,
            HiredApplications = 0,
            WithdrawnApplications = 0
        });

        _screeningStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new ScreeningStatsSnapshot
        {
            TotalScreenings = 0,
            CompletedScreenings = 0,
            PendingScreenings = 0,
            FailedScreenings = 0,
            AverageScore = null,
            AutoAdvancedCount = 0,
            AutoRejectedCount = 0,
            ManualReviewCount = 0
        });

        _matchingStatsReader.GetStatsAsync(Arg.Any<CancellationToken>()).Returns(new MatchingStatsSnapshot
        {
            TotalShortlists = 0,
            DraftShortlists = 0,
            FinalizedShortlists = 0,
            TotalCandidateMatches = 0
        });

        // Act
        await _sut.GetStatsAsync(CancellationToken.None);

        // Assert
        await _recruitmentStatsReader.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
        await _screeningStatsReader.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
        await _matchingStatsReader.Received(1).GetStatsAsync(Arg.Any<CancellationToken>());
    }
}
