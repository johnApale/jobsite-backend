using FluentAssertions;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Errors;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Matching;

public sealed class MatchingServiceTests
{
    private readonly ICandidateMatchRepository _matchRepo = Substitute.For<ICandidateMatchRepository>();
    private readonly MatchingService _service;

    public MatchingServiceTests()
    {
        _service = new MatchingService(
            _matchRepo,
            Substitute.For<ILogger<MatchingService>>());
    }

    // ── GetMatchAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetMatchAsync_ExistingMatch_ReturnsResponse()
    {
        // Arrange
        CandidateMatch match = TestData.CreateCandidateMatch(screeningScore: 85m, compositeScore: 85m, matchStrength: MatchStrength.Strong);
        _matchRepo.GetByApplicationIdAsync(match.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(match);

        // Act
        Modules.Matching.Application.DTOs.CandidateMatchResponse result =
            await _service.GetMatchAsync(match.ApplicationId, CancellationToken.None);

        // Assert
        result.ApplicationId.Should().Be(match.ApplicationId);
        result.ScreeningScore.Should().Be(85m);
        result.CompositeScore.Should().Be(85m);
        result.MatchStrength.Should().Be(MatchStrength.Strong);
    }

    [Fact]
    public async Task GetMatchAsync_NonexistentMatch_ThrowsCandidateMatchNotFound()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _matchRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((CandidateMatch?)null);

        // Act
        Func<Task> act = () => _service.GetMatchAsync(applicationId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── ListMatchesAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ListMatchesAsync_WithJobPostingId_ReturnsMatchesSortedByScore()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        List<CandidateMatch> matches =
        [
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 60m, matchStrength: MatchStrength.Good),
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 90m, matchStrength: MatchStrength.Strong),
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 75m, matchStrength: MatchStrength.Good),
        ];
        _matchRepo.GetByJobPostingIdAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(matches);

        Modules.Matching.Application.DTOs.CandidateMatchQueryParameters parameters = new()
        {
            JobPostingId = jobPostingId
        };

        // Act
        Modules.Matching.Application.DTOs.CandidateMatchListResponse result =
            await _service.ListMatchesAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].CompositeScore.Should().Be(90m);
        result.Items[1].CompositeScore.Should().Be(75m);
        result.Items[2].CompositeScore.Should().Be(60m);
    }

    [Fact]
    public async Task ListMatchesAsync_WithMatchStrengthFilter_ReturnsFilteredResults()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        List<CandidateMatch> matches =
        [
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 90m, matchStrength: MatchStrength.Strong),
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 60m, matchStrength: MatchStrength.Good),
        ];
        _matchRepo.GetByJobPostingIdAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(matches);

        Modules.Matching.Application.DTOs.CandidateMatchQueryParameters parameters = new()
        {
            JobPostingId = jobPostingId,
            MatchStrength = MatchStrength.Strong
        };

        // Act
        Modules.Matching.Application.DTOs.CandidateMatchListResponse result =
            await _service.ListMatchesAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].MatchStrength.Should().Be(MatchStrength.Strong);
    }

    [Fact]
    public async Task ListMatchesAsync_NoJobPostingId_ThrowsValidation()
    {
        // Arrange
        Modules.Matching.Application.DTOs.CandidateMatchQueryParameters parameters = new()
        {
            JobPostingId = null
        };

        // Act
        Func<Task> act = () => _service.ListMatchesAsync(parameters, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(400);
    }
}
