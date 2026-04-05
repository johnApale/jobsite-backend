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
/// Handles <see cref="AssessmentCompletedEvent"/> from the Screening module.
/// Updates the existing <see cref="CandidateMatch"/> with the assessment score
/// and recomputes the composite score.
/// </summary>
public sealed class AssessmentCompletedMatchingHandler : IDomainEventHandler<AssessmentCompletedEvent>
{
    private readonly ICandidateMatchRepository _matchRepository;
    private readonly IScoreAggregationService _scoreAggregation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssessmentCompletedMatchingHandler> _logger;

    public AssessmentCompletedMatchingHandler(
        ICandidateMatchRepository matchRepository,
        IScoreAggregationService scoreAggregation,
        [FromKeyedServices("matching")] IUnitOfWork unitOfWork,
        ILogger<AssessmentCompletedMatchingHandler> logger)
    {
        _matchRepository = matchRepository;
        _scoreAggregation = scoreAggregation;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(AssessmentCompletedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling AssessmentCompletedEvent for application {ApplicationId}, score={Score}",
            domainEvent.ApplicationId, domainEvent.AssessmentScore);

        // Load existing match for update (no AsNoTracking — we need to mutate)
        CandidateMatch? match =
            await _matchRepository.GetByApplicationIdForUpdateAsync(domainEvent.ApplicationId, ct);

        if (match is null)
        {
            _logger.LogWarning(
                "No CandidateMatch found for application {ApplicationId} — " +
                "assessment completed before screening? Skipping.",
                domainEvent.ApplicationId);
            return;
        }

        // Recompute composite score with both screening + assessment
        (decimal compositeScore, string matchStrength) =
            await _scoreAggregation.ComputeCompositeScoreAsync(
                match.ScreeningScore, domainEvent.AssessmentScore, ct);

        match.AssessmentScore = domainEvent.AssessmentScore;
        match.CompositeScore = compositeScore;
        match.MatchStrength = matchStrength;
        match.AssessmentCompletedAt = domainEvent.CompletedAt;
        match.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated CandidateMatch for application {ApplicationId}: " +
            "composite={Score}, strength={Strength} (assessment={Assessment})",
            domainEvent.ApplicationId, compositeScore, matchStrength, domainEvent.AssessmentScore);
    }
}
