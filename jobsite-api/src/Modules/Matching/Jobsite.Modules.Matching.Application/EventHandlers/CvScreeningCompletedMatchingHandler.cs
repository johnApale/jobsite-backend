using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Matching.Application.EventHandlers;

/// <summary>
/// Handles <see cref="CvScreeningCompletedEvent"/> from the Screening module.
/// Creates a <see cref="CandidateMatch"/> when an application passes screening.
/// </summary>
public sealed class CvScreeningCompletedMatchingHandler : IDomainEventHandler<CvScreeningCompletedEvent>
{
    private readonly ICandidateMatchRepository _matchRepository;
    private readonly IScreeningScoreReader _scoreReader;
    private readonly IApplicationDataReader _applicationDataReader;
    private readonly IScoreAggregationService _scoreAggregation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CvScreeningCompletedMatchingHandler> _logger;

    public CvScreeningCompletedMatchingHandler(
        ICandidateMatchRepository matchRepository,
        IScreeningScoreReader scoreReader,
        IApplicationDataReader applicationDataReader,
        IScoreAggregationService scoreAggregation,
        [FromKeyedServices("matching")] IUnitOfWork unitOfWork,
        ILogger<CvScreeningCompletedMatchingHandler> logger)
    {
        _matchRepository = matchRepository;
        _scoreReader = scoreReader;
        _applicationDataReader = applicationDataReader;
        _scoreAggregation = scoreAggregation;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(CvScreeningCompletedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling CvScreeningCompletedEvent for application {ApplicationId}, passed={Passed}",
            domainEvent.ApplicationId, domainEvent.PassedScreening);

        // Skip auto-rejected candidates — they don't enter matching
        if (!domainEvent.PassedScreening)
        {
            _logger.LogInformation(
                "Application {ApplicationId} did not pass screening — skipping matching",
                domainEvent.ApplicationId);
            return;
        }

        // Check if match already exists (idempotency)
        CandidateMatch? existing =
            await _matchRepository.GetByApplicationIdAsync(domainEvent.ApplicationId, ct);
        if (existing is not null)
        {
            _logger.LogWarning(
                "CandidateMatch already exists for application {ApplicationId} — skipping",
                domainEvent.ApplicationId);
            return;
        }

        // Read application data from Recruitment module (JobPostingId, ApplicantUserId)
        ApplicationDataSnapshot? applicationData =
            await _applicationDataReader.GetApplicationDataAsync(domainEvent.ApplicationId, ct);

        if (applicationData is null)
        {
            _logger.LogWarning(
                "No application data found for application {ApplicationId} — skipping matching",
                domainEvent.ApplicationId);
            return;
        }

        // Read screening score from Screening module
        ScreeningScoreSnapshot? score =
            await _scoreReader.GetScoreAsync(domainEvent.ApplicationId, ct);

        if (score is null)
        {
            _logger.LogWarning(
                "No screening score found for application {ApplicationId} — skipping matching",
                domainEvent.ApplicationId);
            return;
        }

        // Compute initial composite score (no assessment yet)
        (decimal compositeScore, string matchStrength) =
            await _scoreAggregation.ComputeCompositeScoreAsync(score.OverallScore, assessmentScore: null, ct);

        DateTime now = DateTime.UtcNow;
        CandidateMatch match = new()
        {
            ApplicationId = domainEvent.ApplicationId,
            JobPostingId = applicationData.JobPostingId,
            ApplicantUserId = applicationData.ApplicantUserId,
            ScreeningScore = score.OverallScore,
            AssessmentScore = null,
            CompositeScore = compositeScore,
            MatchStrength = matchStrength,
            ScreeningCompletedAt = domainEvent.CompletedAt,
            CreatedAt = now,
            UpdatedAt = now
        };

        _matchRepository.Add(match);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created CandidateMatch for application {ApplicationId}: composite={Score}, strength={Strength}",
            domainEvent.ApplicationId, compositeScore, matchStrength);
    }
}
