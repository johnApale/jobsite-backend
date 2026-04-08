using Jobsite.Modules.HRWorkflows.Domain.Entities;

namespace Jobsite.Modules.HRWorkflows.Domain.Interfaces;

public interface IJobOfferRepository
{
    Task<JobOffer?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);
    Task<JobOffer?> GetByApplicationIdForUpdateAsync(Guid applicationId, CancellationToken ct = default);
    Task<List<JobOffer>> GetByExtendedByAsync(Guid userId, CancellationToken ct = default);
    Task<List<JobOffer>> GetExpiredPendingOffersAsync(DateTime cutoff, CancellationToken ct = default);
    void Add(JobOffer offer);
}
