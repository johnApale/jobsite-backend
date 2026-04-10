using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.Modules.Screening.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// End-to-end screening pipeline tests exercising the full flow:
/// deterministic scoring → optional AI scoring → three-tier routing →
/// candidate transparency → persistence.
///
/// Uses real PostgreSQL (Testcontainers) for repositories + real DeterministicScoringEngine.
/// Cross-module readers and AI clients are substituted via NSubstitute.
/// </summary>
[Collection("ScreeningPipeline")]
public sealed class ScreeningPipelineTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly ScreeningPipelineFixture _fixture;

    private ScreeningDbContext _db = null!;
    private ScreeningResultRepository _resultRepo = null!;
    private ScreeningQuestionResponseRepository _responseRepo = null!;

    // Cross-module stubs
    private IJobCriteriaReader _criteriaReader = null!;
    private IApplicantDataReader _applicantDataReader = null!;
    private IJobScreeningQuestionsReader _questionsReader = null!;
    private IApplicationStatusUpdater _statusUpdater = null!;
    private FakeSettingsReader _settingsReader = null!;

    // AI client stubs
    private IEventPublisher _eventPublisher = null!;
    private ITenantIdProvider _tenantIdProvider = null!;

    public ScreeningPipelineTests(ScreeningPipelineFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();

        _db = _fixture.CreateDbContext();
        _resultRepo = new ScreeningResultRepository(_db);
        _responseRepo = new ScreeningQuestionResponseRepository(_db);

        _criteriaReader = Substitute.For<IJobCriteriaReader>();
        _applicantDataReader = Substitute.For<IApplicantDataReader>();
        _questionsReader = Substitute.For<IJobScreeningQuestionsReader>();
        _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
        _settingsReader = new FakeSettingsReader();

        _eventPublisher = Substitute.For<IEventPublisher>();
        _tenantIdProvider = Substitute.For<ITenantIdProvider>();
        _tenantIdProvider.TenantId.Returns(Guid.NewGuid());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    private ScreeningService CreateService()
    {
        DeterministicScoringEngine deterministicEngine = new(Substitute.For<ILogger<DeterministicScoringEngine>>());
        QuestionScoringService questionScoringService = new(
            Substitute.For<ILogger<QuestionScoringService>>());

        return new ScreeningService(
            _resultRepo,
            _responseRepo,
            deterministicEngine,
            _eventPublisher,
            _tenantIdProvider,
            questionScoringService,
            _criteriaReader,
            _questionsReader,
            _applicantDataReader,
            _statusUpdater,
            _settingsReader,
            _db, // ScreeningDbContext implements IUnitOfWork via TenantDbContext
            Substitute.For<ILogger<ScreeningService>>());
    }

    private async Task<Guid> SeedScreeningResultAsync()
    {
        Guid applicationId = Guid.NewGuid();
        ScreeningResult result = new() { ApplicationId = applicationId };
        _resultRepo.Add(result);
        await _db.SaveChangesAsync();
        return applicationId;
    }

    private void ConfigureDefaultSettings(
        decimal autoAdvanceThreshold = 70m,
        decimal autoRejectThreshold = 30m,
        string manualReviewPolicy = "QueueForReview",
        bool aiScoringEnabled = false,
        bool candidateTransparencyEnabled = false,
        string candidateTransparencyLevel = "Summary")
    {
        _settingsReader.Configure(new
        {
            AutoAdvanceThreshold = (double)autoAdvanceThreshold,
            AutoRejectThreshold = (double)autoRejectThreshold,
            ManualReviewPolicy = manualReviewPolicy,
            AiScoringEnabled = aiScoringEnabled,
            CandidateTransparencyEnabled = candidateTransparencyEnabled,
            CandidateTransparencyLevel = candidateTransparencyLevel
        });
    }

    // ── Test: High Score → AutoAdvanced ──────────────────────────────────

    [Fact]
    public async Task ProcessScreening_HighScore_AutoAdvancesApplication()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m, autoRejectThreshold: 30m);

        // Criteria: applicant has exact match for "C#" → 100 score
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                    Configuration = """{"skill_name": "C#"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["C#", ".NET", "Azure"]""",
                ResumeParsedText = "Senior C# developer with 10 years experience"
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — read the result back from the database
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ScreeningStatus.Completed);
        persisted.OverallScore.Should().Be(100m);
        persisted.MatchStrength.Should().Be(MatchStrength.Strong);
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        persisted.CriteriaScoreBreakdown.Should().NotBeNull();
        persisted.CompletedAt.Should().NotBeNull();

        // Verify the application was advanced to Shortlisted (no AfterScreening questions)
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
    }

    // ── Test: Low Score → AutoRejected ───────────────────────────────────

    [Fact]
    public async Task ProcessScreening_LowScore_AutoRejectsApplication()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m, autoRejectThreshold: 30m);

        // Criteria: applicant does NOT have "Golang" → 0 score (below reject threshold)
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "Golang", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                    Configuration = """{"skill_name": "Golang"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["C#", "Python"]""",
                ResumeParsedText = "Python developer focusing on web APIs"
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ScreeningStatus.Completed);
        persisted.OverallScore.Should().Be(0m);
        persisted.MatchStrength.Should().Be(MatchStrength.Weak);
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoRejected);

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", "Score below auto-reject threshold", "Screening",
            Arg.Any<CancellationToken>());
    }

    // ── Test: Middle Score → ManualReview ─────────────────────────────────

    [Fact]
    public async Task ProcessScreening_MiddleScore_RoutesToManualReview()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(
            autoAdvanceThreshold: 70m, autoRejectThreshold: 30m,
            manualReviewPolicy: ManualReviewPolicy.QueueForReview);

        // 50% match: applicant has C# but not Azure → weighted average ~50
        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 50m,
                    Configuration = """{"skill_name": "C#"}"""
                },
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "Azure", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = false, Weight = 50m,
                    Configuration = """{"skill_name": "Azure"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["C#"]""",
                ResumeParsedText = "C# developer, no cloud experience"
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ScreeningStatus.Completed);
        persisted.OverallScore.Should().Be(50m);
        persisted.MatchStrength.Should().Be(MatchStrength.Moderate);
        persisted.Outcome.Should().Be(ScreeningOutcome.ManualReview);

        // ManualReview → status stays as "Screening"
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Screening", null, null, Arg.Any<CancellationToken>());
    }

    // ── Test: AI Scoring Publishes Event via Broker ─────────────────────

    [Fact]
    public async Task ProcessScreening_AiScoringEnabled_PublishesScreeningEvaluationEvent()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        Guid criterionId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m, autoRejectThreshold: 30m, aiScoringEnabled: true);

        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = criterionId, Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(criteria);

        ApplicantDataSnapshot applicant = new()
        {
            UserId = applicantUserId,
            ResumeExtractedSkills = """["C#"]""",
            ResumeParsedText = "C# developer"
        };
        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(applicant);

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — AI scoring event published, deterministic score present, AI fields null (async)
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.OverallScore.Should().Be(100m); // Deterministic: exact match = 100
        persisted.AiOverallScore.Should().BeNull(); // AI arrives async via consumer
        persisted.AiCriteriaScoreBreakdown.Should().BeNull();
        persisted.CriteriaScoreBreakdown.Should().NotBeNull();
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<ScreeningEvaluationRequested>(e => e.ApplicationId == applicationId),
            Arg.Any<CancellationToken>());
    }

    // ── Test: AI Scoring Disabled → No Event Published ────────────────────

    [Fact]
    public async Task ProcessScreening_AiScoringDisabled_DoesNotPublishEvaluationEvent()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m, autoRejectThreshold: 30m, aiScoringEnabled: false);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                    Configuration = """{"skill_name": "C#"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId, ResumeExtractedSkills = """["C#"]"""
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — deterministic score present, no AI event published
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.OverallScore.Should().Be(100m);
        persisted.AiOverallScore.Should().BeNull();
        persisted.AiCriteriaScoreBreakdown.Should().BeNull();
        persisted.Status.Should().Be(ScreeningStatus.Completed);
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);

        await _eventPublisher.DidNotReceive().PublishAsync(
            Arg.Any<ScreeningEvaluationRequested>(), Arg.Any<CancellationToken>());
    }

    // ── Test: Candidate Transparency → Feedback Event Published ──────────

    [Fact]
    public async Task ProcessScreening_TransparencyEnabled_PublishesFeedbackEvent()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(
            autoAdvanceThreshold: 70m, autoRejectThreshold: 30m,
            candidateTransparencyEnabled: true, candidateTransparencyLevel: "Detailed");

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "Python", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                    Configuration = """{"skill_name": "Python"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["Python", "Django"]""",
                ResumeParsedText = "Python developer with Django experience"
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — feedback event published, CandidateFeedback is null (async)
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.CandidateFeedback.Should().BeNull(); // feedback arrives async via consumer

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<FeedbackGenerationRequested>(e =>
                e.ApplicationId == applicationId &&
                e.TransparencyLevel == "Detailed" &&
                e.OverallScore == 100m),
            Arg.Any<CancellationToken>());
    }

    // ── Test: No Applicant Data → Status Failed ───────────────────────────

    [Fact]
    public async Task ProcessScreening_NoApplicantData_SetsStatusFailed()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings();

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CriteriaSnapshot>());

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns((ApplicantDataSnapshot?)null);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — pipeline fails gracefully
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ScreeningStatus.Failed);
        persisted.FailureReason.Should().Contain("No applicant data");
        persisted.OverallScore.Should().BeNull();
        persisted.Outcome.Should().BeNull();
    }

    // ── Test: AutoAdvance with AfterScreening Questions → Assessment ──────

    [Fact]
    public async Task ProcessScreening_HasAfterScreeningQuestions_RoutesToAssessment()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "SQL", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                    Configuration = """{"skill_name": "SQL"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["SQL", "PostgreSQL"]"""
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        // Job has AfterScreening questions → route to Assessment instead of Shortlisted
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(true);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — AutoAdvanced but routed to Assessment (not Shortlisted)
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Assessment", null, null, Arg.Any<CancellationToken>());
    }

    // ── Test: AutoAdvanceAll Policy → Middle Zone Auto-Advances ───────────

    [Fact]
    public async Task ProcessScreening_AutoAdvanceAllPolicy_MiddleScoreAutoAdvances()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(
            autoAdvanceThreshold: 70m, autoRejectThreshold: 30m,
            manualReviewPolicy: ManualReviewPolicy.AutoAdvanceAll);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 50m,
                    Configuration = """{"skill_name": "C#"}"""
                },
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "Go", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = false, Weight = 50m,
                    Configuration = """{"skill_name": "Go"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["C#"]"""
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — 50 is between thresholds, but AutoAdvanceAll policy advances it
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.OverallScore.Should().Be(50m);
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);

        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Shortlisted", null, null, Arg.Any<CancellationToken>());
    }

    // ── Test: Scoring Breakdown JSONB is valid JSON ───────────────────────

    [Fact]
    public async Task ProcessScreening_ScoringBreakdown_IsValidSerializedJson()
    {
        // Arrange
        Guid applicationId = await SeedScreeningResultAsync();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        ConfigureDefaultSettings(autoAdvanceThreshold: 70m);

        _criteriaReader.GetCriteriaForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "Docker", Category = "Skill",
                    EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 60m,
                    Configuration = """{"skill_name": "Docker"}"""
                },
                new CriteriaSnapshot
                {
                    Id = Guid.NewGuid(), Name = "3+ years experience", Category = "Experience",
                    EvaluationMethod = "RangeMatch", IsRequired = false, Weight = 40m,
                    Configuration = """{"min_years": 3, "skill_name": "Docker"}"""
                }
            ]);

        _applicantDataReader.GetApplicantDataAsync(applicantUserId, null, Arg.Any<CancellationToken>())
            .Returns(new ApplicantDataSnapshot
            {
                UserId = applicantUserId,
                ResumeExtractedSkills = """["Docker", "Kubernetes"]""",
                ResumeParsedText = "Docker for 5 years, Kubernetes for 2 years"
            });

        _questionsReader.GetQuestionsForJobAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<QuestionSnapshot>());
        _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(false);

        ScreeningService service = CreateService();

        // Act
        await service.ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, null, CancellationToken.None);

        // Assert — the breakdown JSONB should be valid, parseable JSON
        ScreeningResult? persisted = await _resultRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.CriteriaScoreBreakdown.Should().NotBeNull();

        List<CriterionScoreDto>? breakdown = JsonSerializer.Deserialize<List<CriterionScoreDto>>(
            persisted.CriteriaScoreBreakdown!, JsonOptions);
        breakdown.Should().NotBeNull();
        breakdown.Should().HaveCount(2);
        breakdown![0].CriterionName.Should().Be("Docker");
        breakdown[0].Score.Should().BeGreaterThanOrEqualTo(0m);
        breakdown.All(b => ScoreResult.IsValid(b.Result)).Should().BeTrue();
    }
}


/// <summary>
/// A manual fake for <see cref="ITenantSettingsReader"/> that JSON-round-trips
/// a configured object into whatever <typeparamref name="T"/> the caller requests.
/// This avoids NSubstitute generic type matching issues with private projection types.
/// </summary>
sealed class FakeSettingsReader : ITenantSettingsReader
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private object? _settings;

    public void Configure(object settings) => _settings = settings;

    public Task<T?> GetSettingAsync<T>(string section, CancellationToken ct = default) where T : class
    {
        if (_settings is null)
            return Task.FromResult<T?>(default);

        // Serialize the anonymous/plain object and deserialize into the requested T,
        // allowing conversion to private types like ScreeningSettingsProjection.
        string json = JsonSerializer.Serialize(_settings, s_jsonOptions);
        T? result = JsonSerializer.Deserialize<T>(json, s_jsonOptions);
        return Task.FromResult(result);
    }
}
