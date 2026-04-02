using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Handles integration events received from the message broker.
/// Each consumer module implements this for events it subscribes to.
/// MassTransit auto-discovers implementations via assembly scanning.
/// </summary>
public interface IEventConsumer<in T> where T : class, IIntegrationEvent
{
    /// <summary>Process an integration event received from the broker.</summary>
    Task HandleAsync(T @event, CancellationToken ct = default);
}
