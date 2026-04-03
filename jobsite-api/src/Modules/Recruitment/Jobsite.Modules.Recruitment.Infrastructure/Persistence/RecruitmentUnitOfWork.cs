using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence;

/// <summary>
/// Unit-of-work implementation for the Recruitment tenant DB.
/// </summary>
public sealed class RecruitmentUnitOfWork : IUnitOfWork
{
    private readonly RecruitmentDbContext _db;

    public RecruitmentUnitOfWork(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
