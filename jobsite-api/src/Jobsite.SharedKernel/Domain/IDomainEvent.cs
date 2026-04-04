namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Marker interface for in-process domain events dispatched via the domain event bus.
/// Modules communicate through domain events defined in SharedKernel.
/// </summary>
public interface IDomainEvent;
