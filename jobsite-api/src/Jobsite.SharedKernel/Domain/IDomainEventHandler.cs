namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Handles a domain event of type <typeparamref name="T"/>.
/// Implementations are discovered by assembly scanning and registered in DI.
/// </summary>
public interface IDomainEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T domainEvent, CancellationToken ct);
}
