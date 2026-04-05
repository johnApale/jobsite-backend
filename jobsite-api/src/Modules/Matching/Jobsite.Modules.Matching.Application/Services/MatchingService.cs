using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>
/// Orchestrates candidate match reads — get/list with cursor pagination.
/// </summary>
public sealed class MatchingService : IMatchingService
{
    private readonly ICandidateMatchRepository _matchRepository;
    private readonly ILogger<MatchingService> _logger;

    public MatchingService(
        ICandidateMatchRepository matchRepository,
        ILogger<MatchingService> logger)
    {
        _matchRepository = matchRepository;
        _logger = logger;
    }

    public async Task<CandidateMatchResponse> GetMatchAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        CandidateMatch? match = await _matchRepository.GetByApplicationIdAsync(applicationId, ct);
        if (match is null)
            throw AppErrors.CandidateMatchNotFound;

        return MapToResponse(match);
    }

    public async Task<CandidateMatchListResponse> ListMatchesAsync(
        CandidateMatchQueryParameters parameters, CancellationToken ct = default)
    {
        if (parameters.JobPostingId is null)
            throw AppErrors.Validation.WithMessage("job_posting_id is required");

        List<CandidateMatch> matches =
            await _matchRepository.GetByJobPostingIdAsync(parameters.JobPostingId.Value, ct);

        // Filter by match strength if provided
        if (!string.IsNullOrEmpty(parameters.MatchStrength))
        {
            matches = matches.Where(m => m.MatchStrength == parameters.MatchStrength).ToList();
        }

        // Sort by composite score descending
        matches = matches.OrderByDescending(m => m.CompositeScore).ToList();

        List<CandidateMatchResponse> items = matches.Select(MapToResponse).ToList();

        return new CandidateMatchListResponse
        {
            Items = items,
            NextCursor = null,
            HasMore = false
        };
    }

    internal static CandidateMatchResponse MapToResponse(CandidateMatch match)
    {
        return new CandidateMatchResponse
        {
            ApplicationId = match.ApplicationId,
            JobPostingId = match.JobPostingId,
            ApplicantUserId = match.ApplicantUserId,
            ScreeningScore = match.ScreeningScore,
            AssessmentScore = match.AssessmentScore,
            CompositeScore = match.CompositeScore,
            MatchStrength = match.MatchStrength,
            Rank = match.Rank,
            ScreeningCompletedAt = match.ScreeningCompletedAt,
            AssessmentCompletedAt = match.AssessmentCompletedAt,
            CreatedAt = match.CreatedAt,
            UpdatedAt = match.UpdatedAt
        };
    }
}
