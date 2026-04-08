using Jobsite.Modules.HRWorkflows.Domain.Entities;

namespace Jobsite.Modules.HRWorkflows.Domain.Interfaces;

public interface IFinalInterviewRepository
{
    Task<FinalInterview?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);
    Task<FinalInterview?> GetByApplicationIdForUpdateAsync(Guid applicationId, CancellationToken ct = default);
    Task<List<FinalInterview>> GetByScheduledByAsync(Guid userId, CancellationToken ct = default);
    Task<List<FinalInterview>> GetUpcomingAsync(CancellationToken ct = default);
    void Add(FinalInterview interview);
}
