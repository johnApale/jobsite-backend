using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Matching.Infrastructure.Persistence;

public sealed class MatchingUnitOfWork : IUnitOfWork
{
    private readonly MatchingDbContext _db;

    public MatchingUnitOfWork(MatchingDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
