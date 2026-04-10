using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Repositories;

public sealed class JobOfferRepository : IJobOfferRepository
{
    private readonly HRWorkflowsDbContext _db;

    public JobOfferRepository(HRWorkflowsDbContext db)
    {
        _db = db;
    }

    public async Task<JobOffer?> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.JobOffers
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ApplicationId == applicationId, ct);
    }

    public async Task<JobOffer?> GetByApplicationIdForUpdateAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.JobOffers
            .FirstOrDefaultAsync(o => o.ApplicationId == applicationId, ct);
    }

    public async Task<List<JobOffer>> GetByExtendedByAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _db.JobOffers
            .AsNoTracking()
            .Where(o => o.ExtendedBy == userId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<JobOffer>> GetExpiredPendingOffersAsync(DateTime cutoff, CancellationToken ct = default)
    {
        return await _db.JobOffers
            .Where(o => o.Status == OfferStatus.Pending
                && o.ExpiresAt != null
                && o.ExpiresAt < cutoff)
            .ToListAsync(ct);
    }

    public void Add(JobOffer offer)
    {
        _db.JobOffers.Add(offer);
    }
}
