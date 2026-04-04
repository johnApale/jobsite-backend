using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.Services;

public sealed class AssessmentService : IAssessmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly IScreeningResultRepository _resultRepository;
    private readonly IScreeningQuestionResponseRepository _responseRepository;
    private readonly IJobScreeningQuestionsReader _questionsReader;
    private readonly IApplicationStatusUpdater _statusUpdater;
    private readonly ITenantSettingsReader _settingsReader;
    private readonly QuestionScoringService _questionScoringService;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssessmentService> _logger;

    public AssessmentService(
        IScreeningResultRepository resultRepository,
        IScreeningQuestionResponseRepository responseRepository,
        IJobScreeningQuestionsReader questionsReader,
        IApplicationStatusUpdater statusUpdater,
        ITenantSettingsReader settingsReader,
        QuestionScoringService questionScoringService,
        IDomainEventDispatcher dispatcher,
        [FromKeyedServices("screening")] IUnitOfWork unitOfWork,
        ILogger<AssessmentService> logger)
    {
        _resultRepository = resultRepository;
        _responseRepository = responseRepository;
        _questionsReader = questionsReader;
        _statusUpdater = statusUpdater;
        _settingsReader = settingsReader;
        _questionScoringService = questionScoringService;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SubmitAssessmentAsync(
        Guid applicationId, Guid jobPostingId, Guid applicantUserId,
        SubmitAssessmentRequest request, CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (result is null)
            throw AppErrors.ScreeningResultNotFound;

        if (result.AssessmentScore is not null)
            throw AppErrors.AssessmentAlreadySubmitted;

        // Store AfterScreening responses
        List<QuestionSnapshot> questions = await _questionsReader.GetQuestionsForJobAsync(jobPostingId, ct);
        List<QuestionSnapshot> afterScreeningQuestions = questions
            .Where(q => q.Timing == "AfterScreening")
            .ToList();

        DateTime now = DateTime.UtcNow;
        List<ScreeningQuestionResponse> responses = new();

        foreach (AssessmentAnswerDto answer in request.Answers)
        {
            bool alreadyExists = await _responseRepository.ExistsByApplicationAndQuestionAsync(
                applicationId, answer.QuestionId, ct);
            if (alreadyExists)
                continue;

            ScreeningQuestionResponse response = new()
            {
                ApplicationId = applicationId,
                QuestionId = answer.QuestionId,
                ResponseText = answer.ResponseText,
                ResponseData = answer.ResponseData,
                SubmittedAt = now
            };
            _responseRepository.Add(response);
            responses.Add(response);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // Score AfterScreening responses
        List<AnswerScore> scores = await _questionScoringService.ScoreResponsesAsync(
            responses, afterScreeningQuestions, ct);

        // Calculate assessment score (average of scored answers)
        if (scores.Count > 0)
        {
            result.AssessmentScore = scores.Average(s => s.Score);
        }
        else
        {
            result.AssessmentScore = 0m;
        }

        result.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(ct);

        // Apply completion policy from settings
        AssessmentSettingsProjection? assessmentSettings =
            await _settingsReader.GetSettingAsync<AssessmentSettingsProjection>("assessment_settings", ct);

        string completionPolicy = assessmentSettings?.CompletionPolicy ?? "AutoAdvance";

        if (completionPolicy == "AutoAdvance")
        {
            await _statusUpdater.UpdateStatusAsync(applicationId, "Shortlisted", null, null, ct);
        }
        else
        {
            // QueueForReview — leave in Assessment status for manual review
            await _statusUpdater.UpdateStatusAsync(applicationId, "Assessment", null, null, ct);
        }

        // Publish event
        await _dispatcher.DispatchAsync(new AssessmentCompletedEvent
        {
            ApplicationId = applicationId,
            JobPostingId = jobPostingId,
            ApplicantUserId = applicantUserId,
            AssessmentScore = result.AssessmentScore.Value,
            CompletedAt = now
        }, ct);

        _logger.LogInformation(
            "Assessment submitted for application {ApplicationId}: score={Score}",
            applicationId, result.AssessmentScore);
    }

    public async Task<AssessmentStatusResponse> GetAssessmentStatusAsync(
        Guid applicationId, Guid jobPostingId, CancellationToken ct = default)
    {
        ScreeningResult? result = await _resultRepository.GetByApplicationIdAsync(applicationId, ct);

        bool hasAfterScreeningQuestions = await _questionsReader.HasAfterScreeningQuestionsAsync(jobPostingId, ct);
        if (!hasAfterScreeningQuestions)
        {
            throw AppErrors.AssessmentNotAvailable;
        }

        List<QuestionSnapshot> questions = await _questionsReader.GetQuestionsForJobAsync(jobPostingId, ct);
        List<QuestionSnapshot> afterScreeningQuestions = questions
            .Where(q => q.Timing == "AfterScreening")
            .ToList();

        bool isSubmitted = result?.AssessmentScore is not null;

        return new AssessmentStatusResponse
        {
            ApplicationId = applicationId,
            IsSubmitted = isSubmitted,
            AssessmentScore = result?.AssessmentScore,
            Questions = isSubmitted
                ? []
                : afterScreeningQuestions.Select(q => new AssessmentQuestionDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.QuestionText,
                    QuestionType = q.QuestionType,
                    Options = q.Options,
                    IsRequired = q.IsRequired
                }).ToList()
        };
    }

    private sealed class AssessmentSettingsProjection
    {
        public string CompletionPolicy { get; set; } = "AutoAdvance";
    }
}
