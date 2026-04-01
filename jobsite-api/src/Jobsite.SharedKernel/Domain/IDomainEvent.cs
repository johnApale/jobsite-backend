using MediatR;

namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Marker interface for in-process domain events dispatched via MediatR.
/// Modules communicate through domain events defined in SharedKernel.
/// </summary>
public interface IDomainEvent : INotification;
