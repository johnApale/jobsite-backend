using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public sealed class OfferService : IOfferService
{
    private readonly IJobOfferRepository _offerRepository;
    private readonly IApplicationStatusUpdater _statusUpdater;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OfferService> _logger;

    public OfferService(
        IJobOfferRepository offerRepository,
        IApplicationStatusUpdater statusUpdater,
        IDomainEventDispatcher dispatcher,
        [FromKeyedServices("hr_workflows")] IUnitOfWork unitOfWork,
        ILogger<OfferService> logger)
    {
        _offerRepository = offerRepository;
        _statusUpdater = statusUpdater;
        _dispatcher = dispatcher;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<JobOfferResponse> CreateOfferAsync(
        CreateOfferRequest request, Guid extendedByUserId, CancellationToken ct = default)
    {
        JobOffer? existing = await _offerRepository.GetByApplicationIdAsync(request.ApplicationId, ct);
        if (existing is not null)
            throw AppErrors.OfferAlreadyExists;

        DateTime now = DateTime.UtcNow;
        JobOffer offer = new()
        {
            ApplicationId = request.ApplicationId,
            ClientCompanyId = request.ClientCompanyId,
            Status = OfferStatus.Draft,
            Salary = request.Salary,
            SalaryCurrency = request.SalaryCurrency,
            SalaryPeriod = request.SalaryPeriod,
            EmploymentType = request.EmploymentType,
            StartDate = request.StartDate,
            Benefits = request.Benefits,
            AdditionalTerms = request.AdditionalTerms,
            OfferLetterUrl = request.OfferLetterUrl,
            ExpiresAt = request.ExpiresAt,
            ExtendedBy = extendedByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _offerRepository.Add(offer);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created draft offer for application {ApplicationId}", request.ApplicationId);

        return MapToResponse(offer);
    }

    public async Task<JobOfferResponse> GetOfferAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        JobOffer? offer = await _offerRepository.GetByApplicationIdAsync(applicationId, ct);
        if (offer is null)
            throw AppErrors.OfferNotFound;

        return MapToResponse(offer);
    }

    public async Task<OfferListResponse> ListOffersAsync(
        OfferQueryParameters parameters, CancellationToken ct = default)
    {
        // Use ExtendedBy listing — for now return all via upcoming pattern
        List<JobOffer> offers = await _offerRepository.GetByExtendedByAsync(Guid.Empty, ct);

        if (!string.IsNullOrEmpty(parameters.Status))
        {
            offers = offers.Where(o => o.Status == parameters.Status).ToList();
        }

        List<JobOfferResponse> items = offers.Select(MapToResponse).ToList();

        return new OfferListResponse
        {
            Items = items,
            NextCursor = null,
            HasMore = false
        };
    }

    public async Task<JobOfferResponse> UpdateOfferAsync(
        Guid applicationId, UpdateOfferRequest request, CancellationToken ct = default)
    {
        JobOffer? offer = await _offerRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (offer is null)
            throw AppErrors.OfferNotFound;

        if (offer.Status != OfferStatus.Draft)
            throw AppErrors.OfferNotInDraft;

        if (request.Salary.HasValue)
            offer.Salary = request.Salary.Value;
        if (request.SalaryCurrency is not null)
            offer.SalaryCurrency = request.SalaryCurrency;
        if (request.SalaryPeriod is not null)
            offer.SalaryPeriod = request.SalaryPeriod;
        if (request.EmploymentType is not null)
            offer.EmploymentType = request.EmploymentType;
        if (request.StartDate.HasValue)
            offer.StartDate = request.StartDate.Value;
        if (request.Benefits is not null)
            offer.Benefits = request.Benefits;
        if (request.AdditionalTerms is not null)
            offer.AdditionalTerms = request.AdditionalTerms;
        if (request.OfferLetterUrl is not null)
            offer.OfferLetterUrl = request.OfferLetterUrl;
        if (request.ExpiresAt.HasValue)
            offer.ExpiresAt = request.ExpiresAt.Value;

        offer.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated offer terms for application {ApplicationId}", applicationId);

        return MapToResponse(offer);
    }

    public async Task<JobOfferResponse> ExtendOfferAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        JobOffer? offer = await _offerRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (offer is null)
            throw AppErrors.OfferNotFound;

        if (offer.Status != OfferStatus.Draft)
            throw AppErrors.OfferNotInDraft;

        DateTime now = DateTime.UtcNow;
        offer.Status = OfferStatus.Pending;
        offer.ExtendedAt = now;
        offer.UpdatedAt = now;

        await _statusUpdater.UpdateStatusAsync(
            applicationId,
            "Offered",
            rejectionReason: null,
            rejectedAtStage: null,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        await _dispatcher.DispatchAsync(new OfferExtendedEvent
        {
            ApplicationId = applicationId,
            OfferId = applicationId,
            OfferedAt = now
        }, ct);

        _logger.LogInformation("Extended offer for application {ApplicationId}", applicationId);

        return MapToResponse(offer);
    }

    public async Task<JobOfferResponse> RespondToOfferAsync(
        Guid applicationId, RespondToOfferRequest request, CancellationToken ct = default)
    {
        JobOffer? offer = await _offerRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (offer is null)
            throw AppErrors.OfferNotFound;

        if (offer.Status != OfferStatus.Pending)
            throw AppErrors.OfferNotPending;

        DateTime now = DateTime.UtcNow;
        offer.RespondedAt = now;
        offer.UpdatedAt = now;

        if (request.Accepted)
        {
            offer.Status = OfferStatus.Accepted;

            await _statusUpdater.UpdateStatusAsync(
                applicationId,
                "Hired",
                rejectionReason: null,
                rejectedAtStage: null,
                ct);
        }
        else
        {
            offer.Status = OfferStatus.Declined;
            offer.DeclineReason = request.DeclineReason;

            await _statusUpdater.UpdateStatusAsync(
                applicationId,
                "Rejected",
                rejectionReason: request.DeclineReason,
                rejectedAtStage: "Offered",
                ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Candidate responded to offer for application {ApplicationId}: {Status}",
            applicationId, offer.Status);

        return MapToResponse(offer);
    }

    public async Task WithdrawOfferAsync(
        Guid applicationId, WithdrawOfferRequest request, CancellationToken ct = default)
    {
        JobOffer? offer = await _offerRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (offer is null)
            throw AppErrors.OfferNotFound;

        if (offer.Status is not (OfferStatus.Draft or OfferStatus.Pending))
            throw AppErrors.OfferAlreadyResponded;

        DateTime now = DateTime.UtcNow;
        offer.Status = OfferStatus.Withdrawn;
        offer.WithdrawnAt = now;
        offer.WithdrawalReason = request.WithdrawalReason;
        offer.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Withdrew offer for application {ApplicationId}", applicationId);
    }

    internal static JobOfferResponse MapToResponse(JobOffer offer)
    {
        return new JobOfferResponse
        {
            ApplicationId = offer.ApplicationId,
            ClientCompanyId = offer.ClientCompanyId,
            Status = offer.Status,
            Salary = offer.Salary,
            SalaryCurrency = offer.SalaryCurrency,
            SalaryPeriod = offer.SalaryPeriod,
            EmploymentType = offer.EmploymentType,
            StartDate = offer.StartDate,
            Benefits = offer.Benefits,
            AdditionalTerms = offer.AdditionalTerms,
            OfferLetterUrl = offer.OfferLetterUrl,
            ExpiresAt = offer.ExpiresAt,
            ExtendedBy = offer.ExtendedBy,
            ExtendedAt = offer.ExtendedAt,
            RespondedAt = offer.RespondedAt,
            DeclineReason = offer.DeclineReason,
            WithdrawnAt = offer.WithdrawnAt,
            WithdrawalReason = offer.WithdrawalReason,
            CreatedAt = offer.CreatedAt,
            UpdatedAt = offer.UpdatedAt
        };
    }
}
