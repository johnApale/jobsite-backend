using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence;

public sealed class ScreeningUnitOfWork : IUnitOfWork
{
    private readonly ScreeningDbContext _db;

    public ScreeningUnitOfWork(ScreeningDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
