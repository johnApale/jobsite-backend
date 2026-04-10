using Jobsite.Modules.Screening.Application.EventHandlers;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class ApplicationSubmittedScreeningHandlerTests
{
    private readonly IScreeningResultRepository _resultRepo = Substitute.For<IScreeningResultRepository>();
    private readonly IScreeningQuestionResponseRepository _responseRepo = Substitute.For<IScreeningQuestionResponseRepository>();
    private readonly IScreeningService _screeningService = Substitute.For<IScreeningService>();
    private readonly IJobScreeningQuestionsReader _questionsReader = Substitute.For<IJobScreeningQuestionsReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly ApplicationSubmittedScreeningHandler _handler;

    public ApplicationSubmittedScreeningHandlerTests()
    {
        _handler = new ApplicationSubmittedScreeningHandler(
            _resultRepo,
            _responseRepo,
            _screeningService,
            _questionsReader,
            _unitOfWork,
            _dispatcher,
            Substitute.For<ILogger<ApplicationSubmittedScreeningHandler>>());
    }

    private static ApplicationSubmittedEvent CreateEvent(
        Guid? applicationId = null,
        Guid? jobPostingId = null,
        Guid? applicantUserId = null,
        List<QuestionAnswerPayload>? answers = null)
    {
        return new ApplicationSubmittedEvent
        {
            ApplicationId = applicationId ?? Guid.NewGuid(),
            JobPostingId = jobPostingId ?? Guid.NewGuid(),
            ApplicantUserId = applicantUserId ?? Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow,
            QuestionAnswers = answers ?? []
        };
    }

    // ─── ScreeningResult creation ────────────────────────────────────────

    [Fact]
    public async Task Handle_CreatesScreeningResultWithPendingStatus()
    {
        // Arrange
        ApplicationSubmittedEvent notification = CreateEvent();

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoAdvanced,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert — result was added with correct ApplicationId and Pending status
        _resultRepo.Received(1).Add(Arg.Is<ScreeningResult>(r =>
            r.ApplicationId == notification.ApplicationId &&
            r.Status == ScreeningStatus.Pending));
    }

    // ─── AtApplication answers storage ───────────────────────────────────

    [Fact]
    public async Task Handle_WithQuestionAnswers_StoresAllResponses()
    {
        // Arrange
        Guid q1Id = Guid.NewGuid();
        Guid q2Id = Guid.NewGuid();
        ApplicationSubmittedEvent notification = CreateEvent(answers:
        [
            new QuestionAnswerPayload
            {
                QuestionId = q1Id,
                ResponseText = null,
                ResponseData = """{"answer": true}"""
            },
            new QuestionAnswerPayload
            {
                QuestionId = q2Id,
                ResponseText = "My experience is 5 years",
                ResponseData = null
            }
        ]);

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoAdvanced,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert — two response entities added with correct field mapping
        _responseRepo.Received(2).Add(Arg.Any<ScreeningQuestionResponse>());
        _responseRepo.Received(1).Add(Arg.Is<ScreeningQuestionResponse>(r =>
            r.QuestionId == q1Id &&
            r.ApplicationId == notification.ApplicationId &&
            r.ResponseData == """{"answer": true}"""));
        _responseRepo.Received(1).Add(Arg.Is<ScreeningQuestionResponse>(r =>
            r.QuestionId == q2Id &&
            r.ResponseText == "My experience is 5 years"));
    }

    [Fact]
    public async Task Handle_NoQuestionAnswers_StillCreatesResultAndRunsPipeline()
    {
        // Arrange
        ApplicationSubmittedEvent notification = CreateEvent(answers: []);

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoAdvanced,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert
        _resultRepo.Received(1).Add(Arg.Any<ScreeningResult>());
        _responseRepo.DidNotReceive().Add(Arg.Any<ScreeningQuestionResponse>());
        await _screeningService.Received(1).ProcessScreeningAsync(
            notification.ApplicationId, notification.JobPostingId,
            notification.ApplicantUserId, null, Arg.Any<CancellationToken>());
    }

    // ─── Pipeline orchestration ──────────────────────────────────────────

    [Fact]
    public async Task Handle_CallsProcessScreeningAsync()
    {
        // Arrange
        ApplicationSubmittedEvent notification = CreateEvent();

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoAdvanced,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert
        await _screeningService.Received(1).ProcessScreeningAsync(
            notification.ApplicationId,
            notification.JobPostingId,
            notification.ApplicantUserId,
            null,
            Arg.Any<CancellationToken>());
    }

    // ─── Event publishing ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CompletedWithAutoAdvanced_PublishesCvScreeningCompletedWithPassedTrue()
    {
        // Arrange
        ApplicationSubmittedEvent notification = CreateEvent();

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoAdvanced,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert — publishes CvScreeningCompletedEvent with PassedScreening=true
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<CvScreeningCompletedEvent>(e =>
                e.ApplicationId == notification.ApplicationId &&
                e.PassedScreening == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CompletedWithAutoRejected_PublishesCvScreeningCompletedWithPassedFalse()
    {
        // Arrange
        ApplicationSubmittedEvent notification = CreateEvent();

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Completed,
                Outcome = ScreeningOutcome.AutoRejected,
                CompletedAt = DateTime.UtcNow
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert — AutoRejected → PassedScreening=false
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<CvScreeningCompletedEvent>(e =>
                e.ApplicationId == notification.ApplicationId &&
                e.PassedScreening == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ScreeningFailed_DoesNotPublishEvent()
    {
        // Arrange — result status=Failed means no CvScreeningCompletedEvent
        ApplicationSubmittedEvent notification = CreateEvent();

        _resultRepo.GetByApplicationIdAsync(notification.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(new ScreeningResult
            {
                ApplicationId = notification.ApplicationId,
                Status = ScreeningStatus.Failed,
                FailureReason = "No applicant data available"
            });

        // Act
        await _handler.HandleAsync(notification, CancellationToken.None);

        // Assert — no event published for failed screening
        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<CvScreeningCompletedEvent>(), Arg.Any<CancellationToken>());
    }
}
