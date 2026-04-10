using FluentAssertions;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.HRWorkflows;

public sealed class InterviewServiceTests
{
    private readonly IFinalInterviewRepository _interviewRepo = Substitute.For<IFinalInterviewRepository>();
    private readonly IApplicationStatusUpdater _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly IFeedbackAggregationService _feedbackAggregation = Substitute.For<IFeedbackAggregationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly InterviewService _service;

    public InterviewServiceTests()
    {
        _service = new InterviewService(
            _interviewRepo,
            _statusUpdater,
            _dispatcher,
            _feedbackAggregation,
            _unitOfWork,
            Substitute.For<ILogger<InterviewService>>());
    }

    // ── ScheduleInterviewAsync ───────────────────────────────────────────

    [Fact]
    public async Task ScheduleInterview_NewApplication_CreatesInterviewWithPanelists()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid scheduledBy = Guid.NewGuid();
        List<Guid> panelistIds = [Guid.NewGuid(), Guid.NewGuid()];

        _interviewRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((FinalInterview?)null);

        ScheduleInterviewRequest request = new()
        {
            ApplicationId = applicationId,
            InterviewType = InterviewType.Video,
            ScheduledAt = DateTime.UtcNow.AddDays(7),
            DurationMinutes = 60,
            PanelistUserIds = panelistIds
        };

        // Act
        FinalInterviewResponse result = await _service.ScheduleInterviewAsync(
            request, scheduledBy, CancellationToken.None);

        // Assert
        result.ApplicationId.Should().Be(applicationId);
        result.Status.Should().Be(InterviewStatus.Scheduled);
        result.InterviewType.Should().Be(InterviewType.Video);
        result.Panelists.Should().HaveCount(2);
        result.ScheduledBy.Should().Be(scheduledBy);
        _interviewRepo.Received(1).Add(Arg.Any<FinalInterview>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Any<FinalInterviewScheduledEvent>(), Arg.Any<CancellationToken>());
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "FinalInterview", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleInterview_AlreadyExists_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _interviewRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(TestData.CreateFinalInterview(applicationId: applicationId));

        ScheduleInterviewRequest request = new()
        {
            ApplicationId = applicationId,
            InterviewType = InterviewType.InPerson,
            ScheduledAt = DateTime.UtcNow.AddDays(3),
            DurationMinutes = 90,
            PanelistUserIds = [Guid.NewGuid()]
        };

        // Act
        Func<Task> act = () => _service.ScheduleInterviewAsync(
            request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── GetInterviewAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetInterview_Exists_ReturnsResponse()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        _interviewRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        // Act
        FinalInterviewResponse result = await _service.GetInterviewAsync(
            applicationId, CancellationToken.None);

        // Assert
        result.ApplicationId.Should().Be(applicationId);
    }

    [Fact]
    public async Task GetInterview_NotFound_Throws404()
    {
        // Arrange
        _interviewRepo.GetByApplicationIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FinalInterview?)null);

        // Act
        Func<Task> act = () => _service.GetInterviewAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── UpdateInterviewAsync ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateInterview_Scheduled_UpdatesFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        UpdateInterviewRequest request = new()
        {
            InterviewType = InterviewType.Phone,
            DurationMinutes = 45
        };

        // Act
        FinalInterviewResponse result = await _service.UpdateInterviewAsync(
            applicationId, request, CancellationToken.None);

        // Assert
        result.InterviewType.Should().Be(InterviewType.Phone);
        result.DurationMinutes.Should().Be(45);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateInterview_Completed_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(
            applicationId: applicationId, status: InterviewStatus.Completed);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        // Act
        Func<Task> act = () => _service.UpdateInterviewAsync(
            applicationId, new UpdateInterviewRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── SubmitPanelistFeedbackAsync ──────────────────────────────────────

    [Fact]
    public async Task SubmitFeedback_ValidPanelist_SetsFeedbackFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid interviewerId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        InterviewPanelist panelist = TestData.CreateInterviewPanelist(
            interviewId: applicationId, interviewerId: interviewerId);
        interview.Panelists.Add(panelist);

        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        SubmitFeedbackRequest request = new()
        {
            Rating = 4.5m,
            Recommendation = InterviewRecommendation.StrongHire,
            Strengths = "Excellent technical skills",
            Concerns = "None",
            Notes = "Great candidate"
        };

        // Act
        FinalInterviewResponse result = await _service.SubmitPanelistFeedbackAsync(
            applicationId, interviewerId, request, CancellationToken.None);

        // Assert
        panelist.Rating.Should().Be(4.5m);
        panelist.Recommendation.Should().Be(InterviewRecommendation.StrongHire);
        panelist.FeedbackSubmittedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitFeedback_AlreadySubmitted_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid interviewerId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        InterviewPanelist panelist = TestData.CreateInterviewPanelist(
            interviewId: applicationId,
            interviewerId: interviewerId,
            feedbackSubmittedAt: DateTime.UtcNow);
        interview.Panelists.Add(panelist);

        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        SubmitFeedbackRequest request = new()
        {
            Rating = 3.0m,
            Recommendation = InterviewRecommendation.Hire
        };

        // Act
        Func<Task> act = () => _service.SubmitPanelistFeedbackAsync(
            applicationId, interviewerId, request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task SubmitFeedback_PanelistNotFound_Throws404()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        SubmitFeedbackRequest request = new()
        {
            Rating = 3.0m,
            Recommendation = InterviewRecommendation.Hire
        };

        // Act
        Func<Task> act = () => _service.SubmitPanelistFeedbackAsync(
            applicationId, Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task SubmitFeedback_AllPanelistsDone_AutoCompletesInterview()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid interviewerId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);

        // One panelist already submitted
        InterviewPanelist submittedPanelist = TestData.CreateInterviewPanelist(
            interviewId: applicationId,
            rating: 4.0m,
            recommendation: InterviewRecommendation.Hire,
            feedbackSubmittedAt: DateTime.UtcNow);
        interview.Panelists.Add(submittedPanelist);

        // Current panelist — not yet submitted
        InterviewPanelist currentPanelist = TestData.CreateInterviewPanelist(
            interviewId: applicationId, interviewerId: interviewerId);
        interview.Panelists.Add(currentPanelist);

        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        SubmitFeedbackRequest request = new()
        {
            Rating = 4.0m,
            Recommendation = InterviewRecommendation.StrongHire
        };

        // Act
        await _service.SubmitPanelistFeedbackAsync(
            applicationId, interviewerId, request, CancellationToken.None);

        // Assert
        interview.Status.Should().Be(InterviewStatus.Completed);
        interview.CompletedAt.Should().NotBeNull();
    }

    // ── RecordDecisionAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordDecision_PositiveRecommendation_SetsDecisionFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid decidedBy = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(
            applicationId: applicationId, status: InterviewStatus.Completed);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        RecordDecisionRequest request = new()
        {
            OverallRecommendation = InterviewRecommendation.StrongHire,
            DecisionNotes = "Excellent candidate"
        };

        // Act
        FinalInterviewResponse result = await _service.RecordDecisionAsync(
            applicationId, request, decidedBy, CancellationToken.None);

        // Assert
        result.OverallRecommendation.Should().Be(InterviewRecommendation.StrongHire);
        result.DecidedBy.Should().Be(decidedBy);
        result.DecidedAt.Should().NotBeNull();
        await _statusUpdater.DidNotReceive().UpdateStatusAsync(
            applicationId, "Rejected", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordDecision_NegativeRecommendation_RejectsApplication()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(
            applicationId: applicationId, status: InterviewStatus.Completed);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        RecordDecisionRequest request = new()
        {
            OverallRecommendation = InterviewRecommendation.NoHire,
            DecisionNotes = "Did not meet requirements"
        };

        // Act
        await _service.RecordDecisionAsync(
            applicationId, request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", "Did not meet requirements", "FinalInterview",
            Arg.Any<CancellationToken>());
    }

    // ── CancelInterviewAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CancelInterview_Scheduled_SetsCancelledStatus()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(applicationId: applicationId);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        // Act
        await _service.CancelInterviewAsync(applicationId, "Position filled", CancellationToken.None);

        // Assert
        interview.Status.Should().Be(InterviewStatus.Cancelled);
        interview.CancelledAt.Should().NotBeNull();
        interview.CancellationReason.Should().Be("Position filled");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelInterview_AlreadyCompleted_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(
            applicationId: applicationId, status: InterviewStatus.Completed);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        // Act
        Func<Task> act = () => _service.CancelInterviewAsync(
            applicationId, "reason", CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CancelInterview_AlreadyCancelled_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        FinalInterview interview = TestData.CreateFinalInterview(
            applicationId: applicationId, status: InterviewStatus.Cancelled);
        _interviewRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(interview);

        // Act
        Func<Task> act = () => _service.CancelInterviewAsync(
            applicationId, "reason", CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CancelInterview_NotFound_Throws404()
    {
        // Arrange
        _interviewRepo.GetByApplicationIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((FinalInterview?)null);

        // Act
        Func<Task> act = () => _service.CancelInterviewAsync(
            Guid.NewGuid(), "reason", CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }
}
