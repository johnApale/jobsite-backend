using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Publishes integration events to the message broker (RabbitMQ / Azure Service Bus).
/// Implemented by MassTransit in the Api layer.
/// </summary>
public interface IEventPublisher
{
    /// <summary>Publish an integration event to all subscribed consumers.</summary>
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : class, IIntegrationEvent;
}
