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

public sealed class CvScreeningCompletedMatchingHandlerTests
{
    private readonly ICandidateMatchRepository _matchRepo = Substitute.For<ICandidateMatchRepository>();
    private readonly IScreeningScoreReader _scoreReader = Substitute.For<IScreeningScoreReader>();
    private readonly IApplicationDataReader _appDataReader = Substitute.For<IApplicationDataReader>();
    private readonly IScoreAggregationService _scoreAggregation = Substitute.For<IScoreAggregationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CvScreeningCompletedMatchingHandler _handler;

    public CvScreeningCompletedMatchingHandlerTests()
    {
        _handler = new CvScreeningCompletedMatchingHandler(
            _matchRepo,
            _scoreReader,
            _appDataReader,
            _scoreAggregation,
            _unitOfWork,
            Substitute.For<ILogger<CvScreeningCompletedMatchingHandler>>());
    }

    private static CvScreeningCompletedEvent CreateEvent(
        Guid? applicationId = null,
        bool passedScreening = true)
    {
        return new CvScreeningCompletedEvent
        {
            ApplicationId = applicationId ?? Guid.NewGuid(),
            ScreeningResultId = Guid.NewGuid(),
            PassedScreening = passedScreening,
            CompletedAt = DateTime.UtcNow
        };
    }

    // ── Passed screening — creates match ─────────────────────────────────

    [Fact]
    public async Task Handle_PassedScreening_CreatesCandidateMatch()
    {
        // Arrange
        CvScreeningCompletedEvent evt = CreateEvent();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        _matchRepo.GetByApplicationIdAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((CandidateMatch?)null);

        _appDataReader.GetApplicationDataAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDataSnapshot
            {
                ApplicationId = evt.ApplicationId,
                JobPostingId = jobPostingId,
                ApplicantUserId = applicantUserId
            });

        _scoreReader.GetScoreAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningScoreSnapshot
            {
                OverallScore = 82m,
                MatchStrength = MatchStrength.Strong,
                Status = "Completed"
            });

        _scoreAggregation.ComputeCompositeScoreAsync(82m, null, Arg.Any<CancellationToken>())
            .Returns((82m, MatchStrength.Strong));

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        _matchRepo.Received(1).Add(Arg.Is<CandidateMatch>(m =>
            m.ApplicationId == evt.ApplicationId &&
            m.JobPostingId == jobPostingId &&
            m.ApplicantUserId == applicantUserId &&
            m.ScreeningScore == 82m &&
            m.CompositeScore == 82m &&
            m.MatchStrength == MatchStrength.Strong &&
            m.AssessmentScore == null));

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Failed screening — skips ─────────────────────────────────────────

    [Fact]
    public async Task Handle_FailedScreening_DoesNotCreateMatch()
    {
        // Arrange
        CvScreeningCompletedEvent evt = CreateEvent(passedScreening: false);

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        _matchRepo.DidNotReceive().Add(Arg.Any<CandidateMatch>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Idempotency — existing match ─────────────────────────────────────

    [Fact]
    public async Task Handle_MatchAlreadyExists_SkipsCreation()
    {
        // Arrange
        CvScreeningCompletedEvent evt = CreateEvent();
        _matchRepo.GetByApplicationIdAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(TestData.CreateCandidateMatch(applicationId: evt.ApplicationId));

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert — only called once for the check, never calls Add
        _matchRepo.DidNotReceive().Add(Arg.Any<CandidateMatch>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Missing application data — skips ─────────────────────────────────

    [Fact]
    public async Task Handle_NoApplicationData_SkipsCreation()
    {
        // Arrange
        CvScreeningCompletedEvent evt = CreateEvent();
        _matchRepo.GetByApplicationIdAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((CandidateMatch?)null);
        _appDataReader.GetApplicationDataAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ApplicationDataSnapshot?)null);

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        _matchRepo.DidNotReceive().Add(Arg.Any<CandidateMatch>());
    }

    // ── Missing screening score — skips ──────────────────────────────────

    [Fact]
    public async Task Handle_NoScreeningScore_SkipsCreation()
    {
        // Arrange
        CvScreeningCompletedEvent evt = CreateEvent();
        _matchRepo.GetByApplicationIdAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((CandidateMatch?)null);
        _appDataReader.GetApplicationDataAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ApplicationDataSnapshot
            {
                ApplicationId = evt.ApplicationId,
                JobPostingId = Guid.NewGuid(),
                ApplicantUserId = Guid.NewGuid()
            });
        _scoreReader.GetScoreAsync(evt.ApplicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningScoreSnapshot?)null);

        // Act
        await _handler.HandleAsync(evt, CancellationToken.None);

        // Assert
        _matchRepo.DidNotReceive().Add(Arg.Any<CandidateMatch>());
    }
}
