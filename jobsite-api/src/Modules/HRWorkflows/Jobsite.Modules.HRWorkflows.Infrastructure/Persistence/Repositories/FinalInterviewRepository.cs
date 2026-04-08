using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.Repositories;

public sealed class FinalInterviewRepository : IFinalInterviewRepository
{
    private readonly HRWorkflowsDbContext _db;

    public FinalInterviewRepository(HRWorkflowsDbContext db)
    {
        _db = db;
    }

    public async Task<FinalInterview?> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.FinalInterviews
            .AsNoTracking()
            .Include(i => i.Panelists)
            .FirstOrDefaultAsync(i => i.ApplicationId == applicationId, ct);
    }

    public async Task<FinalInterview?> GetByApplicationIdForUpdateAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.FinalInterviews
            .Include(i => i.Panelists)
            .FirstOrDefaultAsync(i => i.ApplicationId == applicationId, ct);
    }

    public async Task<List<FinalInterview>> GetByScheduledByAsync(
        Guid scheduledBy, CancellationToken ct = default)
    {
        return await _db.FinalInterviews
            .AsNoTracking()
            .Include(i => i.Panelists)
            .Where(i => i.ScheduledBy == scheduledBy)
            .OrderByDescending(i => i.ScheduledAt)
            .ToListAsync(ct);
    }

    public async Task<List<FinalInterview>> GetUpcomingAsync(CancellationToken ct = default)
    {
        return await _db.FinalInterviews
            .AsNoTracking()
            .Include(i => i.Panelists)
            .OrderByDescending(i => i.ScheduledAt)
            .ToListAsync(ct);
    }

    public void Add(FinalInterview interview)
    {
        _db.FinalInterviews.Add(interview);
    }
}
