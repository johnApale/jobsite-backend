using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>
/// Manages shortlist generation, candidate management, and finalization.
/// Publishes <see cref="CandidateShortlistedEvent"/> for each candidate on finalization.
/// </summary>
public sealed class ShortlistService : IShortlistService
{
    private readonly IShortlistRepository _shortlistRepository;
    private readonly ICandidateMatchRepository _matchRepository;
    private readonly IApplicationStatusUpdater _statusUpdater;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ITenantSettingsReader _settingsReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ShortlistService> _logger;

    public ShortlistService(
        IShortlistRepository shortlistRepository,
        ICandidateMatchRepository matchRepository,
        IApplicationStatusUpdater statusUpdater,
        IDomainEventDispatcher dispatcher,
        ITenantSettingsReader settingsReader,
        [FromKeyedServices("matching")] IUnitOfWork unitOfWork,
        ILogger<ShortlistService> logger)
    {
        _shortlistRepository = shortlistRepository;
        _matchRepository = matchRepository;
        _statusUpdater = statusUpdater;
        _dispatcher = dispatcher;
        _settingsReader = settingsReader;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ShortlistResponse> GenerateShortlistAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        MatchingSettings settings = await LoadSettingsAsync(ct);

        // Get all candidate matches for this job, ordered by composite score
        List<CandidateMatch> matches =
            await _matchRepository.GetByJobPostingIdAsync(jobPostingId, ct);

        List<CandidateMatch> topCandidates = matches
            .OrderByDescending(m => m.CompositeScore)
            .Take(settings.ShortlistSize)
            .ToList();

        DateTime now = DateTime.UtcNow;
        Shortlist shortlist = new()
        {
            JobPostingId = jobPostingId,
            Status = ShortlistStatus.Draft,
            GeneratedBy = ShortlistCandidateSource.Algorithm,
            TotalCandidates = topCandidates.Count,
            CreatedAt = now,
            UpdatedAt = now
        };

        int rank = 1;
        foreach (CandidateMatch match in topCandidates)
        {
            ShortlistCandidate candidate = new()
            {
                ShortlistId = shortlist.Id,
                ApplicationId = match.ApplicationId,
                ApplicantUserId = match.ApplicantUserId,
                CompositeScore = match.CompositeScore,
                Rank = rank++,
                Source = ShortlistCandidateSource.Algorithm,
                AddedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };
            shortlist.Candidates.Add(candidate);
        }

        _shortlistRepository.Add(shortlist);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Generated shortlist {ShortlistId} for job {JobPostingId} with {Count} candidates",
            shortlist.Id, jobPostingId, topCandidates.Count);

        return MapToResponse(shortlist);
    }

    public async Task<ShortlistResponse> GetShortlistAsync(
        Guid shortlistId, CancellationToken ct = default)
    {
        Shortlist? shortlist = await _shortlistRepository.GetByIdAsync(shortlistId, ct);
        if (shortlist is null)
            throw AppErrors.ShortlistNotFound;

        return MapToResponse(shortlist);
    }

    public async Task<ShortlistListResponse> ListShortlistsAsync(
        ShortlistQueryParameters parameters, CancellationToken ct = default)
    {
        if (parameters.JobPostingId is null)
            throw AppErrors.Validation.WithMessage("job_posting_id is required");

        List<Shortlist> shortlists =
            await _shortlistRepository.GetByJobPostingIdAsync(parameters.JobPostingId.Value, ct);

        if (!string.IsNullOrEmpty(parameters.Status))
        {
            shortlists = shortlists.Where(s => s.Status == parameters.Status).ToList();
        }

        List<ShortlistSummaryResponse> items = shortlists
            .OrderByDescending(s => s.CreatedAt)
            .Select(MapToSummary)
            .ToList();

        return new ShortlistListResponse
        {
            Items = items,
            NextCursor = null,
            HasMore = false
        };
    }

    public async Task<ShortlistResponse> AddCandidateAsync(
        Guid shortlistId, Guid applicationId, CancellationToken ct = default)
    {
        Shortlist? shortlist = await _shortlistRepository.GetByIdForUpdateAsync(shortlistId, ct);
        if (shortlist is null)
            throw AppErrors.ShortlistNotFound;

        if (shortlist.Status == ShortlistStatus.Finalized)
            throw AppErrors.ShortlistAlreadyFinalized;

        // Check for duplicate (active candidates only)
        bool alreadyExists = shortlist.Candidates.Any(c =>
            c.ApplicationId == applicationId && c.RemovedAt is null);
        if (alreadyExists)
            throw AppErrors.CandidateAlreadyOnShortlist;

        // Get the candidate match for score/user info
        CandidateMatch? match = await _matchRepository.GetByApplicationIdAsync(applicationId, ct);
        if (match is null)
            throw AppErrors.CandidateMatchNotFound;

        int maxRank = shortlist.Candidates
            .Where(c => c.RemovedAt is null)
            .Select(c => c.Rank)
            .DefaultIfEmpty(0)
            .Max();

        DateTime now = DateTime.UtcNow;
        ShortlistCandidate candidate = new()
        {
            ShortlistId = shortlistId,
            ApplicationId = applicationId,
            ApplicantUserId = match.ApplicantUserId,
            CompositeScore = match.CompositeScore,
            Rank = maxRank + 1,
            Source = ShortlistCandidateSource.Manual,
            AddedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        shortlist.Candidates.Add(candidate);
        shortlist.TotalCandidates = shortlist.Candidates.Count(c => c.RemovedAt is null);
        shortlist.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Manually added candidate {ApplicationId} to shortlist {ShortlistId}",
            applicationId, shortlistId);

        return MapToResponse(shortlist);
    }

    public async Task RemoveCandidateAsync(
        Guid shortlistId, Guid applicationId, CancellationToken ct = default)
    {
        Shortlist? shortlist = await _shortlistRepository.GetByIdForUpdateAsync(shortlistId, ct);
        if (shortlist is null)
            throw AppErrors.ShortlistNotFound;

        if (shortlist.Status == ShortlistStatus.Finalized)
            throw AppErrors.ShortlistAlreadyFinalized;

        ShortlistCandidate? candidate = shortlist.Candidates
            .FirstOrDefault(c => c.ApplicationId == applicationId && c.RemovedAt is null);
        if (candidate is null)
            throw AppErrors.ShortlistCandidateNotFound;

        candidate.RemovedAt = DateTime.UtcNow;
        shortlist.TotalCandidates = shortlist.Candidates.Count(c => c.RemovedAt is null);
        shortlist.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Removed candidate {ApplicationId} from shortlist {ShortlistId}",
            applicationId, shortlistId);
    }

    public async Task<ShortlistResponse> FinalizeShortlistAsync(
        Guid shortlistId, Guid userId, CancellationToken ct = default)
    {
        Shortlist? shortlist = await _shortlistRepository.GetByIdForUpdateAsync(shortlistId, ct);
        if (shortlist is null)
            throw AppErrors.ShortlistNotFound;

        if (shortlist.Status == ShortlistStatus.Finalized)
            throw AppErrors.ShortlistAlreadyFinalized;

        DateTime now = DateTime.UtcNow;
        shortlist.Status = ShortlistStatus.Finalized;
        shortlist.FinalizedAt = now;
        shortlist.FinalizedBy = userId;
        shortlist.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(ct);

        // Publish CandidateShortlistedEvent for each active candidate and update application status
        List<ShortlistCandidate> activeCandidates =
            shortlist.Candidates.Where(c => c.RemovedAt is null).ToList();

        foreach (ShortlistCandidate candidate in activeCandidates)
        {
            await _statusUpdater.UpdateStatusAsync(
                candidate.ApplicationId,
                "Shortlisted",
                rejectionReason: null,
                rejectedAtStage: null,
                ct);

            await _dispatcher.DispatchAsync(new CandidateShortlistedEvent
            {
                ApplicationId = candidate.ApplicationId,
                JobPostingId = shortlist.JobPostingId,
                ApplicantUserId = candidate.ApplicantUserId,
                ShortlistedAt = now
            }, ct);
        }

        _logger.LogInformation(
            "Finalized shortlist {ShortlistId} with {Count} candidates for job {JobPostingId}",
            shortlistId, activeCandidates.Count, shortlist.JobPostingId);

        return MapToResponse(shortlist);
    }

    private async Task<MatchingSettings> LoadSettingsAsync(CancellationToken ct)
    {
        MatchingSettings? settings =
            await _settingsReader.GetSettingAsync<MatchingSettings>("matching_settings", ct);

        return settings ?? new MatchingSettings();
    }

    internal static ShortlistResponse MapToResponse(Shortlist shortlist)
    {
        return new ShortlistResponse
        {
            Id = shortlist.Id,
            JobPostingId = shortlist.JobPostingId,
            Status = shortlist.Status,
            GeneratedBy = shortlist.GeneratedBy,
            TotalCandidates = shortlist.TotalCandidates,
            FinalizedAt = shortlist.FinalizedAt,
            FinalizedBy = shortlist.FinalizedBy,
            Candidates = shortlist.Candidates
                .Where(c => c.RemovedAt is null)
                .OrderBy(c => c.Rank)
                .Select(MapCandidateToResponse)
                .ToList(),
            CreatedAt = shortlist.CreatedAt,
            UpdatedAt = shortlist.UpdatedAt
        };
    }

    private static ShortlistSummaryResponse MapToSummary(Shortlist shortlist)
    {
        return new ShortlistSummaryResponse
        {
            Id = shortlist.Id,
            JobPostingId = shortlist.JobPostingId,
            Status = shortlist.Status,
            GeneratedBy = shortlist.GeneratedBy,
            TotalCandidates = shortlist.TotalCandidates,
            FinalizedAt = shortlist.FinalizedAt,
            FinalizedBy = shortlist.FinalizedBy,
            CreatedAt = shortlist.CreatedAt,
            UpdatedAt = shortlist.UpdatedAt
        };
    }

    private static ShortlistCandidateResponse MapCandidateToResponse(ShortlistCandidate candidate)
    {
        return new ShortlistCandidateResponse
        {
            Id = candidate.Id,
            ApplicationId = candidate.ApplicationId,
            ApplicantUserId = candidate.ApplicantUserId,
            CompositeScore = candidate.CompositeScore,
            Rank = candidate.Rank,
            Source = candidate.Source,
            AddedAt = candidate.AddedAt,
            RemovedAt = candidate.RemovedAt
        };
    }
}
