namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Cross-cutting middleware that wraps domain event dispatch.
/// Behaviors execute in registration order, forming a pipeline around the event handlers.
/// </summary>
public interface IDomainEventPipelineBehavior
{
    Task HandleAsync(IDomainEvent domainEvent, Func<Task> next, CancellationToken ct);
}
