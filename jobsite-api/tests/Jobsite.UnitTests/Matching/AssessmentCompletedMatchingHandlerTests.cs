using FluentAssertions;
using Jobsite.Modules.Matching.Application.EventHandlers;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Matching;

public sealed class AssessmentCompletedMatchingHandlerTests
{
    private readonly ICandidateMatchRepository _matchRepo = Substitute.For<ICandidateMatchRepository>();
    private readonly IScoreAggregationService _scoreAggregation = Substitute.For<IScoreAggregationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AssessmentCompletedMatchingHandler _handler;

    public AssessmentCompletedMatchingHandlerTests()
    {
        _handler = new AssessmentCompletedMatchingHandler(
            _matchRepo,
            _scoreAggregation,
            _unitOfWork,
            Substitute.For<ILogger<AssessmentCompletedMatchingHandler>>());
    }

    private static AssessmentCompletedEvent CreateEvent(
        Guid? applicationId = null,
        decimal assessmentScore = 85m)
    {
        return new AssessmentCompletedEvent
        {
            ApplicationId = applicationId ?? Guid.NewGuid(),
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            AssessmentScore = assessmentScore,
            CompletedAt = DateTime.UtcNow
        };
    }

    // ── Updates existing match ───────────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingMatch_UpdatesAssessmentScoreAndRecomputes()
    {
        // Arrange
        AssessmentCompletedEvent evt = CreateEvent(assessmentScore: 90m);
        CandidateMatch match = TestData.CreateCandidateMatch(
            applicationId: evt.ApplicationId,
            screeningScore: 80m,
            compositeScore: 80m,
            matchStrength: MatchStrength.Good);

        _matchRepo.GetByApplicationIdForUpdateAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(match);

        _scoreAggregation.ComputeCompositeScoreAsync(80m, 90m, Arg.Any<CancellationToken>())
            .Returns((84m, MatchStrength.Strong));

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        match.AssessmentScore.Should().Be(90m);
        match.CompositeScore.Should().Be(84m);
        match.MatchStrength.Should().Be(MatchStrength.Strong);
        match.AssessmentCompletedAt.Should().NotBeNull();

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── No existing match — skips ────────────────────────────────────────

    [Fact]
    public async Task Handle_NoExistingMatch_SkipsUpdate()
    {
        // Arrange
        AssessmentCompletedEvent evt = CreateEvent();
        _matchRepo.GetByApplicationIdForUpdateAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((CandidateMatch?)null);

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
