namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Unit-of-work abstraction for transactional persistence.
/// Implemented by each DbContext wrapper. Dispatches domain events after save.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
