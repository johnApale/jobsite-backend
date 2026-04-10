using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class AssessmentServiceTests
{
    private readonly IScreeningResultRepository _resultRepo = Substitute.For<IScreeningResultRepository>();
    private readonly IScreeningQuestionResponseRepository _responseRepo = Substitute.For<IScreeningQuestionResponseRepository>();
    private readonly IJobScreeningQuestionsReader _questionsReader = Substitute.For<IJobScreeningQuestionsReader>();
    private readonly IApplicationStatusUpdater _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
    private readonly ITenantSettingsReader _settingsReader = Substitute.For<ITenantSettingsReader>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AssessmentService _service;

    public AssessmentServiceTests()
    {
        _tenantIdProvider.TenantId.Returns(Guid.NewGuid());

        QuestionScoringService questionScoringService = new(
            Substitute.For<ILogger<QuestionScoringService>>());

        _service = new AssessmentService(
            _resultRepo,
            _responseRepo,
            _questionsReader,
            _statusUpdater,
            _settingsReader,
            questionScoringService,
            _eventPublisher,
            _tenantIdProvider,
            _dispatcher,
            _unitOfWork,
            Substitute.For<ILogger<AssessmentService>>());
    }

    private static ScreeningResult CreateResult(
        Guid? applicationId = null,
        decimal? assessmentScore = null)
    {
        return new ScreeningResult
        {
            ApplicationId = applicationId ?? Guid.NewGuid(),
            Status = ScreeningStatus.Completed,
            Outcome = ScreeningOutcome.AutoAdvanced,
            OverallScore = 80m,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m,
            AssessmentScore = assessmentScore
        };
    }

    private static QuestionSnapshot CreateAfterScreeningQuestion(
        Guid id, string type, string? expectedAnswer = null)
    {
        return new QuestionSnapshot
        {
            Id = id,
            QuestionText = "Test question",
            QuestionType = type,
            Timing = "AfterScreening",
            IsRequired = true,
            Weight = 10,
            ExpectedAnswer = expectedAnswer
        };
    }

    // ─── SubmitAssessmentAsync ────────────────────────────────────────────

    [Fact]
    public async Task SubmitAssessmentAsync_ValidAnswers_CalculatesAverageAssessmentScore()
    {
        // Arrange — 3 YesNo answers → scores 100, 100, 0 → average = 66.67
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid q1 = Guid.NewGuid();
        Guid q2 = Guid.NewGuid();
        Guid q3 = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        List<QuestionSnapshot> questions =
        [
            CreateAfterScreeningQuestion(q1, "YesNo", """{"correct": true}"""),
            CreateAfterScreeningQuestion(q2, "YesNo", """{"correct": false}"""),
            CreateAfterScreeningQuestion(q3, "YesNo", """{"correct": true}""")
        ];
        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(questions);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // defaults: AutoAdvance

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = jobPostingId,
            Answers =
            [
                new AssessmentAnswerDto { QuestionId = q1, ResponseData = """{"answer": true}""" },
                new AssessmentAnswerDto { QuestionId = q2, ResponseData = """{"answer": false}""" },
                new AssessmentAnswerDto { QuestionId = q3, ResponseData = """{"answer": false}""" }
            ]
        };

        // Act
        await _service.SubmitAssessmentAsync(
            applicationId, jobPostingId, Guid.NewGuid(), request, CancellationToken.None);

        // Assert — scores: q1=100, q2=100, q3=0 → average = (100+100+0)/3 ≈ 66.67
        result.AssessmentScore.Should().NotBeNull();
        result.AssessmentScore!.Value.Should().BeApproximately(66.67m, 0.01m);
    }

    [Fact]
    public async Task SubmitAssessmentAsync_AutoAdvancePolicy_UpdatesStatusToShortlisted()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid q1 = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>
            {
                CreateAfterScreeningQuestion(q1, "YesNo", """{"correct": true}""")
            });

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // default = AutoAdvance

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = jobPostingId,
            Answers = [new AssessmentAnswerDto { QuestionId = q1, ResponseData = """{"answer": true}""" }]
        };

        // Act
        await _service.SubmitAssessmentAsync(
            applicationId, jobPostingId, Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAssessmentAsync_PublishesAssessmentCompletedEvent()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        Guid q1 = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>
            {
                CreateAfterScreeningQuestion(q1, "YesNo", """{"correct": true}""")
            });

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = jobPostingId,
            Answers = [new AssessmentAnswerDto { QuestionId = q1, ResponseData = """{"answer": true}""" }]
        };

        // Act
        await _service.SubmitAssessmentAsync(
            applicationId, jobPostingId, applicantUserId, request, CancellationToken.None);

        // Assert
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<AssessmentCompletedEvent>(e =>
                e.ApplicationId == applicationId &&
                e.JobPostingId == jobPostingId &&
                e.ApplicantUserId == applicantUserId &&
                e.AssessmentScore == result.AssessmentScore!.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAssessmentAsync_AlreadySubmitted_ThrowsConflict()
    {
        // Arrange — result already has an assessment score
        Guid applicationId = Guid.NewGuid();
        ScreeningResult result = CreateResult(applicationId, assessmentScore: 80m);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers = [new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseData = """{"answer": true}""" }]
        };

        // Act
        Func<Task> act = () => _service.SubmitAssessmentAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        // Assert — duplicate submission returns 409
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 409);
    }

    [Fact]
    public async Task SubmitAssessmentAsync_ResultNotFound_Throws404()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningResult?)null);

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers = [new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseData = """{"answer": true}""" }]
        };

        // Act
        Func<Task> act = () => _service.SubmitAssessmentAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task SubmitAssessmentAsync_DuplicateAnswer_SkipsExistingResponse()
    {
        // Arrange — one answer already exists, one is new
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid existingQuestionId = Guid.NewGuid();
        Guid newQuestionId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, existingQuestionId, Arg.Any<CancellationToken>())
            .Returns(true); // already exists
        _responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, newQuestionId, Arg.Any<CancellationToken>())
            .Returns(false);

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>
            {
                CreateAfterScreeningQuestion(existingQuestionId, "YesNo", """{"correct": true}"""),
                CreateAfterScreeningQuestion(newQuestionId, "YesNo", """{"correct": true}""")
            });

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        SubmitAssessmentRequest request = new()
        {
            JobPostingId = jobPostingId,
            Answers =
            [
                new AssessmentAnswerDto { QuestionId = existingQuestionId, ResponseData = """{"answer": true}""" },
                new AssessmentAnswerDto { QuestionId = newQuestionId, ResponseData = """{"answer": true}""" }
            ]
        };

        // Act
        await _service.SubmitAssessmentAsync(
            applicationId, jobPostingId, Guid.NewGuid(), request, CancellationToken.None);

        // Assert — only the new response was added
        _responseRepo.Received(1).Add(Arg.Is<ScreeningQuestionResponse>(r =>
            r.QuestionId == newQuestionId));
        _responseRepo.DidNotReceive().Add(Arg.Is<ScreeningQuestionResponse>(r =>
            r.QuestionId == existingQuestionId));
    }

    // ─── GetAssessmentStatusAsync ────────────────────────────────────────

    [Fact]
    public async Task GetAssessmentStatusAsync_NotSubmitted_ReturnsPendingWithQuestions()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid q1 = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);
        _resultRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(true);
        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>
            {
                CreateAfterScreeningQuestion(q1, "FreeText")
            });

        // Act
        AssessmentStatusResponse response = await _service.GetAssessmentStatusAsync(
            applicationId, jobPostingId, CancellationToken.None);

        // Assert
        response.IsSubmitted.Should().BeFalse();
        response.AssessmentScore.Should().BeNull();
        response.Questions.Should().HaveCount(1);
        response.Questions[0].QuestionId.Should().Be(q1);
    }

    [Fact]
    public async Task GetAssessmentStatusAsync_AlreadySubmitted_ReturnsSubmittedWithScore()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId, assessmentScore: 85m);
        _resultRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(true);
        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());

        // Act
        AssessmentStatusResponse response = await _service.GetAssessmentStatusAsync(
            applicationId, jobPostingId, CancellationToken.None);

        // Assert
        response.IsSubmitted.Should().BeTrue();
        response.AssessmentScore.Should().Be(85m);
        response.Questions.Should().BeEmpty(); // empty when already submitted
    }

    [Fact]
    public async Task GetAssessmentStatusAsync_NoAfterScreeningQuestions_Throws422()
    {
        // Arrange — job has no AfterScreening questions → assessment not available
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();

        _resultRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(CreateResult(applicationId));

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Func<Task> act = () => _service.GetAssessmentStatusAsync(
            applicationId, jobPostingId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 422);
    }
}
