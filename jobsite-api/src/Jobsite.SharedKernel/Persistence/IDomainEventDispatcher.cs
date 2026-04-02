namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Dispatches domain events after persistence.
/// Implemented in the API project using MediatR's <c>IPublisher</c>.
/// Defined here so <see cref="TenantDbContext"/> can dispatch without
/// depending on the full MediatR package.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(Domain.IDomainEvent domainEvent, CancellationToken ct = default);
}
