namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Marker interface for integration events published to the message broker
/// (RabbitMQ / Azure Service Bus). Used for cross-service communication
/// between the monolith and the AI Interview Service.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Unique event identifier for idempotency.</summary>
    Guid EventId { get; }

    /// <summary>When the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Correlation ID for distributed tracing.</summary>
    string CorrelationId { get; }
}
