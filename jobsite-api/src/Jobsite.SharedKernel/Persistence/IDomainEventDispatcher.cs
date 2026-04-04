namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Dispatches domain events after persistence.
/// Implemented in the API project using the in-process domain event bus.
/// Defined here so <see cref="TenantDbContext"/> can dispatch without
/// depending on the event bus implementation.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(Domain.IDomainEvent domainEvent, CancellationToken ct = default);
}
