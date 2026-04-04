using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Jobsite.UnitTests.Screening;

public sealed class ScreeningServiceTests
{
    private readonly IScreeningResultRepository _resultRepo = Substitute.For<IScreeningResultRepository>();
    private readonly IScreeningQuestionResponseRepository _responseRepo = Substitute.For<IScreeningQuestionResponseRepository>();
    private readonly IDeterministicScoringEngine _deterministicEngine = Substitute.For<IDeterministicScoringEngine>();
    private readonly IAiScoringClient _aiScoringClient = Substitute.For<IAiScoringClient>();
    private readonly IAiCandidateFeedbackClient _feedbackClient = Substitute.For<IAiCandidateFeedbackClient>();
    private readonly IAiAnswerScoringClient _aiAnswerScoringClient = Substitute.For<IAiAnswerScoringClient>();
    private readonly IJobCriteriaReader _criteriaReader = Substitute.For<IJobCriteriaReader>();
    private readonly IJobScreeningQuestionsReader _questionsReader = Substitute.For<IJobScreeningQuestionsReader>();
    private readonly IApplicantDataReader _applicantDataReader = Substitute.For<IApplicantDataReader>();
    private readonly IApplicationStatusUpdater _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
    private readonly ITenantSettingsReader _settingsReader = Substitute.For<ITenantSettingsReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ScreeningService _service;

    public ScreeningServiceTests()
    {
        QuestionScoringService questionScoringService = new(
            _aiAnswerScoringClient,
            Substitute.For<ILogger<QuestionScoringService>>());

        _service = new ScreeningService(
            _resultRepo,
            _responseRepo,
            _deterministicEngine,
            _aiScoringClient,
            _feedbackClient,
            questionScoringService,
            _criteriaReader,
            _questionsReader,
            _applicantDataReader,
            _statusUpdater,
            _settingsReader,
            _unitOfWork,
            Substitute.For<ILogger<ScreeningService>>());
    }

    private static ScreeningResult CreateResult(
        Guid? applicationId = null,
        string status = ScreeningStatus.Pending,
        string? outcome = null,
        decimal? overallScore = null)
    {
        return new ScreeningResult
        {
            ApplicationId = applicationId ?? Guid.NewGuid(),
            Status = status,
            Outcome = outcome,
            OverallScore = overallScore,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
    }

    // ─── GetResultAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetResultAsync_ExistingResult_ReturnsResponse()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ScreeningResult result = CreateResult(applicationId, ScreeningStatus.Completed,
            ScreeningOutcome.AutoAdvanced, 85m);

        _resultRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        ScreeningResultResponse response = await _service.GetResultAsync(applicationId, CancellationToken.None);

        // Assert
        response.ApplicationId.Should().Be(applicationId);
        response.Status.Should().Be(ScreeningStatus.Completed);
        response.OverallScore.Should().Be(85m);
        response.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
    }

    [Fact]
    public async Task GetResultAsync_NotFound_ThrowsAppError()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _resultRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningResult?)null);

        // Act
        Func<Task> act = () => _service.GetResultAsync(applicationId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 404);
    }

    // ─── ListResultsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ListResultsAsync_DelegatesToRepository()
    {
        // Arrange
        ScreeningResultQueryParameters parameters = new() { PageSize = 10 };
        ScreeningResultListResponse expected = new()
        {
            Items = [],
            HasMore = false
        };

        _resultRepo.ListAsync(parameters, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        ScreeningResultListResponse response = await _service.ListResultsAsync(parameters, CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expected);
        await _resultRepo.Received(1).ListAsync(parameters, Arg.Any<CancellationToken>());
    }

    // ─── ManualReviewAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ManualReviewAsync_ManuallyAdvanced_UpdatesOutcomeAndStatus()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid reviewerId = Guid.NewGuid();
        ScreeningResult result = CreateResult(applicationId, ScreeningStatus.Completed,
            ScreeningOutcome.ManualReview, 50m);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        ManualReviewRequest request = new()
        {
            Outcome = ScreeningOutcome.ManuallyAdvanced,
            ReviewNotes = "Good candidate upon manual review"
        };

        // Act
        ScreeningResultResponse response = await _service.ManualReviewAsync(
            applicationId, request, reviewerId, CancellationToken.None);

        // Assert
        response.Outcome.Should().Be(ScreeningOutcome.ManuallyAdvanced);
        response.ReviewedBy.Should().Be(reviewerId);
        response.ReviewNotes.Should().Be("Good candidate upon manual review");

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManualReviewAsync_ManuallyRejected_UpdatesOutcomeAndStatus()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid reviewerId = Guid.NewGuid();
        ScreeningResult result = CreateResult(applicationId, ScreeningStatus.Completed,
            ScreeningOutcome.ManualReview, 45m);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        ManualReviewRequest request = new()
        {
            Outcome = ScreeningOutcome.ManuallyRejected,
            ReviewNotes = "Does not meet minimum requirements"
        };

        // Act
        ScreeningResultResponse response = await _service.ManualReviewAsync(
            applicationId, request, reviewerId, CancellationToken.None);

        // Assert
        response.Outcome.Should().Be(ScreeningOutcome.ManuallyRejected);
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", "Rejected during manual review", "Screening", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ManualReviewAsync_NotFound_ThrowsAppError()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningResult?)null);

        ManualReviewRequest request = new()
        {
            Outcome = ScreeningOutcome.ManuallyAdvanced,
            ReviewNotes = "Test"
        };

        // Act
        Func<Task> act = () => _service.ManualReviewAsync(
            applicationId, request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task ManualReviewAsync_NotInManualReview_ThrowsUnprocessableEntity()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ScreeningResult result = CreateResult(applicationId, ScreeningStatus.Completed,
            ScreeningOutcome.AutoAdvanced, 90m);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        ManualReviewRequest request = new()
        {
            Outcome = ScreeningOutcome.ManuallyAdvanced,
            ReviewNotes = "Test"
        };

        // Act
        Func<Task> act = () => _service.ManualReviewAsync(
            applicationId, request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>();
    }

    // ─── ProcessScreeningAsync ───────────────────────────────────────────

    [Fact]
    public async Task ProcessScreeningAsync_HighScore_AutoAdvances()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // default config: threshold 70/30

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new()
        {
            UserId = applicantUserId,
            ResumeExtractedSkills = """["C#"]"""
        };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 85m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ScreeningStatus.Completed);
        result.OverallScore.Should().Be(85m);
        result.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_LowScore_AutoRejects()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "Rust", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "Rust"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new()
        {
            UserId = applicantUserId,
            ResumeExtractedSkills = """["C#"]"""
        };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 10m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ScreeningStatus.Completed);
        result.OverallScore.Should().Be(10m);
        result.Outcome.Should().Be(ScreeningOutcome.AutoRejected);

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", Arg.Any<string>(), "Screening", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_HighScore_WithAfterScreeningQuestions_RoutesToAssessment()
    {
        // Arrange — score above threshold + job has AfterScreening questions → Assessment (not Shortlisted)
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new()
        {
            UserId = applicantUserId,
            ResumeExtractedSkills = """["C#"]"""
        };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 90m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(true); // This is the key difference

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Assessment", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_MidScore_QueueForReview_QueuesManualReview()
    {
        // Arrange — score between thresholds + QueueForReview policy → ManualReview outcome
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // defaults: threshold 70/30, QueueForReview

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "Skills", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 50m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ScreeningOutcome.ManualReview);
        result.MatchStrength.Should().Be(MatchStrength.Moderate); // 40-59 range
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Screening", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_ScoreExactlyAtAdvanceThreshold_AutoAdvances()
    {
        // Arrange — score exactly at the threshold boundary (>=)
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // default threshold = 70

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 70m, Breakdown = [] }); // exactly at threshold

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — score >= threshold means AutoAdvanced
        result.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_ScoreExactlyAtRejectThreshold_AutoRejects()
    {
        // Arrange — score exactly at the reject boundary (<=)
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null); // default threshold = 30

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(criteria, applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 30m, Breakdown = [] }); // exactly at threshold

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — score <= threshold means AutoRejected
        result.Outcome.Should().Be(ScreeningOutcome.AutoRejected);
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", Arg.Any<string>(), "Screening", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessScreeningAsync_PipelineException_SetsStatusFailed()
    {
        // Arrange — deterministic engine throws → result is marked Failed with reason
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(Arg.Any<List<CriteriaSnapshot>>(), applicant, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Scoring engine failed"));

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ScreeningStatus.Failed);
        result.FailureReason.Should().Contain("Scoring engine failed");
    }

    [Fact]
    public async Task ProcessScreeningAsync_NoApplicantData_MarksFailed()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = CreateResult(applicationId);

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns((ApplicantDataSnapshot?)null);

        // Act
        await _service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ScreeningStatus.Failed);
        result.FailureReason.Should().Contain("No applicant data");
    }

    [Fact]
    public async Task ProcessScreeningAsync_NotFound_ThrowsAppError()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningResult?)null);

        // Act
        Func<Task> act = () => _service.ProcessScreeningAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 404);
    }

    // ─── MapToResponse ───────────────────────────────────────────────────

    [Fact]
    public void MapToResponse_MapsAllProperties()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid reviewerId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Completed,
            OverallScore = 75.5m,
            MatchStrength = Jobsite.Modules.Screening.Domain.Constants.MatchStrength.Good,
            Outcome = ScreeningOutcome.AutoAdvanced,
            CriteriaScoreBreakdown = """[{"score": 80}]""",
            AiCriteriaScoreBreakdown = """[{"score": 70}]""",
            AiOverallScore = 72m,
            QuestionScoreBreakdown = """[{"score": 90}]""",
            AssessmentScore = 88m,
            CandidateFeedback = "Strong profile",
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m,
            ReviewedBy = reviewerId,
            ReviewedAt = now,
            ReviewNotes = "Looks good",
            StartedAt = now.AddMinutes(-5),
            CompletedAt = now
        };

        // Act
        ScreeningResultResponse response = ScreeningService.MapToResponse(result);

        // Assert
        response.ApplicationId.Should().Be(applicationId);
        response.Status.Should().Be(ScreeningStatus.Completed);
        response.OverallScore.Should().Be(75.5m);
        response.MatchStrength.Should().Be(Jobsite.Modules.Screening.Domain.Constants.MatchStrength.Good);
        response.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        response.AiOverallScore.Should().Be(72m);
        response.AssessmentScore.Should().Be(88m);
        response.ReviewedBy.Should().Be(reviewerId);
        response.AutoAdvanceThreshold.Should().Be(70m);
        response.AutoRejectThreshold.Should().Be(30m);
    }

    // ─── RescoreApplicationAsync ─────────────────────────────────────────

    [Fact]
    public async Task RescoreApplicationAsync_CompletedResult_ResetsFieldsAndRerunsScoring()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Completed,
            OverallScore = 85m,
            MatchStrength = Jobsite.Modules.Screening.Domain.Constants.MatchStrength.Strong,
            Outcome = ScreeningOutcome.AutoAdvanced,
            CriteriaScoreBreakdown = """[{"score": 85}]""",
            AiOverallScore = 80m,
            AiCriteriaScoreBreakdown = """[{"ai_score": 80}]""",
            QuestionScoreBreakdown = """[{"q": 1}]""",
            CandidateFeedback = "Strong match",
            ReviewedBy = Guid.NewGuid(),
            ReviewedAt = DateTime.UtcNow,
            ReviewNotes = "Old notes",
            FailureReason = null,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow
        };

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(Arg.Any<List<CriteriaSnapshot>>(), applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 55m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _service.RescoreApplicationAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — old fields were reset and new pipeline result is applied
        result.OverallScore.Should().Be(55m);
        result.AiOverallScore.Should().BeNull();
        result.AiCriteriaScoreBreakdown.Should().BeNull();
        result.ReviewedBy.Should().BeNull();
        result.ReviewedAt.Should().BeNull();
        result.ReviewNotes.Should().BeNull();
        result.Status.Should().Be(ScreeningStatus.Completed);
    }

    [Fact]
    public async Task RescoreApplicationAsync_FailedResult_CanBeRescored()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Failed,
            FailureReason = "Previous error"
        };

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(Arg.Any<List<CriteriaSnapshot>>(), applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 90m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _service.RescoreApplicationAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — previously failed result is now successfully completed with new score
        result.Status.Should().Be(ScreeningStatus.Completed);
        result.OverallScore.Should().Be(90m);
        result.FailureReason.Should().BeNull();
        result.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
    }

    [Fact]
    public async Task RescoreApplicationAsync_PendingResult_ThrowsUnprocessable()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Pending
        };

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        Func<Task> act = () => _service.RescoreApplicationAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 422);
    }

    [Fact]
    public async Task RescoreApplicationAsync_InProgressResult_ThrowsUnprocessable()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.InProgress
        };

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        Func<Task> act = () => _service.RescoreApplicationAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 422);
    }

    [Fact]
    public async Task RescoreApplicationAsync_NotFound_ThrowsNotFound()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((ScreeningResult?)null);

        // Act
        Func<Task> act = () => _service.RescoreApplicationAsync(
            applicationId, Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task RescoreApplicationAsync_ChangesRouting_WhenNewScoreCrossesThreshold()
    {
        // Arrange — was AutoRejected (score 25), now rescores to 80 → AutoAdvanced
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Completed,
            OverallScore = 25m,
            Outcome = ScreeningOutcome.AutoRejected,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };

        _resultRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(result);

        _settingsReader.GetSettingAsync<object>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((object?)null);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        ApplicantDataSnapshot applicant = new() { UserId = applicantUserId };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _deterministicEngine.ScoreAsync(Arg.Any<List<CriteriaSnapshot>>(), applicant, Arg.Any<CancellationToken>())
            .Returns(new ScoringResult { OverallScore = 80m, Breakdown = [] });

        _responseRepo.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", Arg.Any<CancellationToken>())
            .Returns(new List<ScreeningQuestionResponse>());

        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _service.RescoreApplicationAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — routing changed from AutoRejected → AutoAdvanced (Shortlisted)
        result.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        result.OverallScore.Should().Be(80m);
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
