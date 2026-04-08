using Jobsite.Modules.HRWorkflows.Application.DTOs;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public interface IOfferService
{
    Task<JobOfferResponse> CreateOfferAsync(CreateOfferRequest request, Guid extendedByUserId, CancellationToken ct = default);
    Task<JobOfferResponse> GetOfferAsync(Guid applicationId, CancellationToken ct = default);
    Task<OfferListResponse> ListOffersAsync(OfferQueryParameters parameters, CancellationToken ct = default);
    Task<JobOfferResponse> UpdateOfferAsync(Guid applicationId, UpdateOfferRequest request, CancellationToken ct = default);
    Task<JobOfferResponse> ExtendOfferAsync(Guid applicationId, CancellationToken ct = default);
    Task<JobOfferResponse> RespondToOfferAsync(Guid applicationId, RespondToOfferRequest request, CancellationToken ct = default);
    Task WithdrawOfferAsync(Guid applicationId, WithdrawOfferRequest request, CancellationToken ct = default);
}
