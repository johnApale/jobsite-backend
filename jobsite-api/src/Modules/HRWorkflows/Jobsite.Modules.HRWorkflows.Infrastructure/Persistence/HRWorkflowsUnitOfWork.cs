using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;

public sealed class HRWorkflowsUnitOfWork : IUnitOfWork
{
    private readonly HRWorkflowsDbContext _db;

    public HRWorkflowsUnitOfWork(HRWorkflowsDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
