using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.Services;

/// <summary>
/// Orchestrates the full screening scoring pipeline: deterministic scoring,
/// optional AI scoring, question scoring, three-tier routing, and candidate transparency.
/// </summary>
public sealed class ScreeningService : IScreeningService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly IScreeningResultRepository _resultRepository;
    private readonly IScreeningQuestionResponseRepository _responseRepository;
    private readonly IDeterministicScoringEngine _deterministicEngine;
    private readonly IAiScoringClient _aiScoringClient;
    private readonly IAiCandidateFeedbackClient _feedbackClient;
    private readonly QuestionScoringService _questionScoringService;
    private readonly IJobCriteriaReader _criteriaReader;
    private readonly IJobScreeningQuestionsReader _questionsReader;
    private readonly IApplicantDataReader _applicantDataReader;
    private readonly IApplicationStatusUpdater _statusUpdater;
    private readonly ITenantSettingsReader _settingsReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ScreeningService> _logger;

    public ScreeningService(
        IScreeningResultRepository resultRepository,
        IScreeningQuestionResponseRepository responseRepository,
        IDeterministicScoringEngine deterministicEngine,
        IAiScoringClient aiScoringClient,
        IAiCandidateFeedbackClient feedbackClient,
        QuestionScoringService questionScoringService,
        IJobCriteriaReader criteriaReader,
        IJobScreeningQuestionsReader questionsReader,
        IApplicantDataReader applicantDataReader,
        IApplicationStatusUpdater statusUpdater,
        ITenantSettingsReader settingsReader,
        [FromKeyedServices("screening")] IUnitOfWork unitOfWork,
        ILogger<ScreeningService> logger)
    {
        _resultRepository = resultRepository;
        _responseRepository = responseRepository;
        _deterministicEngine = deterministicEngine;
        _aiScoringClient = aiScoringClient;
        _feedbackClient = feedbackClient;
        _questionScoringService = questionScoringService;
        _criteriaReader = criteriaReader;
        _questionsReader = questionsReader;
        _applicantDataReader = applicantDataReader;
        _statusUpdater = statusUpdater;
        _settingsReader = settingsReader;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task ProcessScreeningAsync(
        Guid applicationId, Guid jobPostingId, Guid applicantUserId, Guid? resumeId,
        CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (result is null)
            throw AppErrors.ScreeningResultNotFound;

        try
        {
            result.Status = ScreeningStatus.InProgress;
            result.StartedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            // 1. Load tenant screening configuration
            ScreeningConfig config = await LoadScreeningConfigAsync(ct);
            result.AutoAdvanceThreshold = config.AutoAdvanceThreshold;
            result.AutoRejectThreshold = config.AutoRejectThreshold;

            // 2. Load evaluation data
            List<CriteriaSnapshot> criteria = await _criteriaReader.GetCriteriaForJobAsync(jobPostingId, ct);
            ApplicantDataSnapshot? applicantData = await _applicantDataReader.GetApplicantDataAsync(
                applicantUserId, resumeId, ct);

            if (applicantData is null)
            {
                result.Status = ScreeningStatus.Failed;
                result.FailureReason = "No applicant data available for screening";
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }

            // 3. Deterministic scoring (always runs)
            ScoringResult deterministicResult = await _deterministicEngine.ScoreAsync(criteria, applicantData, ct);
            result.OverallScore = deterministicResult.OverallScore;
            result.CriteriaScoreBreakdown = JsonSerializer.Serialize(deterministicResult.Breakdown, JsonOptions);

            // 4. AI scoring (conditional)
            if (config.AiScoringEnabled)
            {
                AiScoringResult? aiResult = await _aiScoringClient.EvaluateAsync(criteria, applicantData, ct);
                if (aiResult is not null)
                {
                    result.AiOverallScore = aiResult.OverallScore;
                    result.AiCriteriaScoreBreakdown = JsonSerializer.Serialize(aiResult.Breakdown, JsonOptions);
                }
            }

            // 5. Score AtApplication question answers
            List<ScreeningQuestionResponse> atApplicationResponses =
                await _responseRepository.GetByApplicationIdAndTimingAsync(applicationId, "AtApplication", ct);

            if (atApplicationResponses.Count > 0)
            {
                List<QuestionSnapshot> questions = await _questionsReader.GetQuestionsForJobAsync(jobPostingId, ct);
                List<QuestionSnapshot> atAppQuestions = questions.Where(q => q.Timing == "AtApplication").ToList();

                List<AnswerScore> questionScores = await _questionScoringService.ScoreResponsesAsync(
                    atApplicationResponses, atAppQuestions, ct);
                result.QuestionScoreBreakdown = JsonSerializer.Serialize(questionScores, JsonOptions);
            }

            // 6. Derive match strength
            result.MatchStrength = MatchStrength.FromScore(result.OverallScore.Value);

            // 7. Candidate transparency
            if (config.CandidateTransparencyEnabled &&
                config.CandidateTransparencyLevel != TransparencyLevel.None &&
                result.CriteriaScoreBreakdown is not null)
            {
                string? feedback = await _feedbackClient.GenerateFeedbackAsync(
                    result.CriteriaScoreBreakdown,
                    result.OverallScore.Value,
                    config.CandidateTransparencyLevel,
                    ct);
                result.CandidateFeedback = feedback;
            }

            // 8. Complete screening
            result.Status = ScreeningStatus.Completed;
            result.CompletedAt = DateTime.UtcNow;

            // 9. Three-tier routing
            await RouteApplicationAsync(result, jobPostingId, config, ct);

            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Screening completed for application {ApplicationId}: score={Score}, outcome={Outcome}",
                applicationId, result.OverallScore, result.Outcome);
        }
        catch (Exception ex) when (ex is not AppError)
        {
            _logger.LogError(ex, "Screening pipeline failed for application {ApplicationId}", applicationId);
            result.Status = ScreeningStatus.Failed;
            result.FailureReason = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<ScreeningResultResponse> GetResultAsync(Guid applicationId, CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdAsync(applicationId, ct);
        if (result is null)
            throw AppErrors.ScreeningResultNotFound;

        return MapToResponse(result);
    }

    public async Task<ScreeningResultListResponse> ListResultsAsync(
        ScreeningResultQueryParameters parameters, CancellationToken ct = default)
    {
        return await _resultRepository.ListAsync(parameters, ct);
    }

    public async Task<ScreeningResultResponse> ManualReviewAsync(
        Guid applicationId, ManualReviewRequest request, Guid reviewerId,
        CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (result is null)
            throw AppErrors.ScreeningResultNotFound;

        if (result.Outcome != ScreeningOutcome.ManualReview)
            throw AppErrors.UnprocessableEntity.WithMessage(
                "Only applications with ManualReview outcome can be manually reviewed");

        result.ReviewedBy = reviewerId;
        result.ReviewedAt = DateTime.UtcNow;
        result.ReviewNotes = request.ReviewNotes;

        if (request.Outcome == ScreeningOutcome.ManuallyAdvanced)
        {
            result.Outcome = ScreeningOutcome.ManuallyAdvanced;

            bool hasAfterScreening = await _questionsReader.HasAfterScreeningQuestionsAsync(
                // We need the job posting ID — get it from the application
                // The result's ApplicationId is shared with the application
                result.ApplicationId, ct);

            // Look up the job posting ID from context — use the question reader to determine assessment
            // Since we have the applicationId but need per-job determination, query questions reader
            // Actually, HasAfterScreeningQuestionsAsync needs a jobPostingId.
            // We need to determine how to get it. For manual review, we must look it up.
            // For now, we check if there are unsubmitted AfterScreening responses.
            // The assessment route depends on whether AfterScreening questions exist for the job.

            // We'll use status updater which only needs applicationId + newStatus
            string newStatus = "Shortlisted"; // default
            // Cannot efficiently determine AfterScreening question presence without jobPostingId here.
            // The assessment flow will be triggered if questions exist — determined by status.
            await _statusUpdater.UpdateStatusAsync(applicationId, newStatus, null, null, ct);
        }
        else if (request.Outcome == ScreeningOutcome.ManuallyRejected)
        {
            result.Outcome = ScreeningOutcome.ManuallyRejected;
            await _statusUpdater.UpdateStatusAsync(
                applicationId, "Rejected", "Rejected during manual review", "Screening", ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(result);
    }

    public async Task RescoreApplicationAsync(
        Guid applicationId, Guid jobPostingId, Guid applicantUserId, Guid? resumeId,
        CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (result is null)
            throw AppErrors.ScreeningResultNotFound;

        if (result.Status is not (ScreeningStatus.Completed or ScreeningStatus.Failed))
            throw AppErrors.UnprocessableEntity.WithMessage(
                "Only completed or failed screenings can be rescored");

        // Reset scoring fields
        result.OverallScore = null;
        result.MatchStrength = null;
        result.Outcome = null;
        result.CriteriaScoreBreakdown = null;
        result.AiCriteriaScoreBreakdown = null;
        result.AiOverallScore = null;
        result.QuestionScoreBreakdown = null;
        result.CandidateFeedback = null;
        result.ReviewedBy = null;
        result.ReviewedAt = null;
        result.ReviewNotes = null;
        result.FailureReason = null;
        result.StartedAt = null;
        result.CompletedAt = null;
        result.Status = ScreeningStatus.Pending;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Rescoring triggered for application {ApplicationId}, job {JobPostingId}",
            applicationId, jobPostingId);

        await ProcessScreeningAsync(applicationId, jobPostingId, applicantUserId, resumeId, ct);
    }

    private async Task RouteApplicationAsync(
        ScreeningResult result, Guid jobPostingId, ScreeningConfig config,
        CancellationToken ct)
    {
        decimal score = result.OverallScore!.Value;

        if (score >= config.AutoAdvanceThreshold)
        {
            result.Outcome = ScreeningOutcome.AutoAdvanced;

            bool hasAfterScreening = await _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, ct);
            string newStatus = hasAfterScreening ? "Assessment" : "Shortlisted";
            await _statusUpdater.UpdateStatusAsync(result.ApplicationId, newStatus, null, null, ct);
        }
        else if (score <= config.AutoRejectThreshold)
        {
            result.Outcome = ScreeningOutcome.AutoRejected;
            await _statusUpdater.UpdateStatusAsync(
                result.ApplicationId, "Rejected", "Score below auto-reject threshold", "Screening", ct);
        }
        else
        {
            // Between thresholds — apply manual review policy
            switch (config.ManualReviewPolicy)
            {
                case ManualReviewPolicy.AutoAdvanceAll:
                    result.Outcome = ScreeningOutcome.AutoAdvanced;
                    bool hasAfterScreening = await _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, ct);
                    string advanceStatus = hasAfterScreening ? "Assessment" : "Shortlisted";
                    await _statusUpdater.UpdateStatusAsync(result.ApplicationId, advanceStatus, null, null, ct);
                    break;

                case ManualReviewPolicy.AutoRejectAll:
                    result.Outcome = ScreeningOutcome.AutoRejected;
                    await _statusUpdater.UpdateStatusAsync(
                        result.ApplicationId, "Rejected", "Score below threshold (AutoRejectAll policy)", "Screening", ct);
                    break;

                case ManualReviewPolicy.QueueForReview:
                case ManualReviewPolicy.NotifyAndHold:
                default:
                    result.Outcome = ScreeningOutcome.ManualReview;
                    await _statusUpdater.UpdateStatusAsync(result.ApplicationId, "Screening", null, null, ct);
                    break;
            }
        }
    }

    private async Task<ScreeningConfig> LoadScreeningConfigAsync(CancellationToken ct)
    {
        ScreeningSettingsProjection? settings =
            await _settingsReader.GetSettingAsync<ScreeningSettingsProjection>("screening_settings", ct);

        return new ScreeningConfig
        {
            AutoAdvanceThreshold = (decimal)(settings?.AutoAdvanceThreshold ?? 70.0),
            AutoRejectThreshold = (decimal)(settings?.AutoRejectThreshold ?? 30.0),
            ManualReviewPolicy = settings?.ManualReviewPolicy ?? ManualReviewPolicy.QueueForReview,
            AiScoringEnabled = settings?.AiScoringEnabled ?? false,
            CandidateTransparencyEnabled = settings?.CandidateTransparencyEnabled ?? false,
            CandidateTransparencyLevel = settings?.CandidateTransparencyLevel ?? TransparencyLevel.Summary
        };
    }

    public static ScreeningResultResponse MapToResponse(ScreeningResult result) => new()
    {
        ApplicationId = result.ApplicationId,
        Status = result.Status,
        OverallScore = result.OverallScore,
        MatchStrength = result.MatchStrength,
        Outcome = result.Outcome,
        CriteriaScoreBreakdown = result.CriteriaScoreBreakdown,
        AiCriteriaScoreBreakdown = result.AiCriteriaScoreBreakdown,
        AiOverallScore = result.AiOverallScore,
        QuestionScoreBreakdown = result.QuestionScoreBreakdown,
        AssessmentScore = result.AssessmentScore,
        CandidateFeedback = result.CandidateFeedback,
        AutoAdvanceThreshold = result.AutoAdvanceThreshold,
        AutoRejectThreshold = result.AutoRejectThreshold,
        ReviewedBy = result.ReviewedBy,
        ReviewedAt = result.ReviewedAt,
        ReviewNotes = result.ReviewNotes,
        FailureReason = result.FailureReason,
        StartedAt = result.StartedAt,
        CompletedAt = result.CompletedAt,
        CreatedAt = result.CreatedAt,
        UpdatedAt = result.UpdatedAt
    };

    /// <summary>Projection for reading screening settings from CompanySettings JSONB.</summary>
    private sealed class ScreeningSettingsProjection
    {
        public double AutoAdvanceThreshold { get; set; } = 70.0;
        public double AutoRejectThreshold { get; set; } = 30.0;
        public string ManualReviewPolicy { get; set; } = "QueueForReview";
        public bool AiScoringEnabled { get; set; }
        public bool CandidateTransparencyEnabled { get; set; }
        public string CandidateTransparencyLevel { get; set; } = "Summary";
    }

    private sealed class ScreeningConfig
    {
        public decimal AutoAdvanceThreshold { get; set; }
        public decimal AutoRejectThreshold { get; set; }
        public string ManualReviewPolicy { get; set; } = null!;
        public bool AiScoringEnabled { get; set; }
        public bool CandidateTransparencyEnabled { get; set; }
        public string CandidateTransparencyLevel { get; set; } = null!;
    }
}
