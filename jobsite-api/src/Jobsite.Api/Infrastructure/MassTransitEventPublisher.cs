using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using MassTransit;

namespace Jobsite.Api.Infrastructure;

/// <summary>
/// Publishes integration events to RabbitMQ via MassTransit.
/// </summary>
public sealed class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventPublisher> _logger;

    public MassTransitEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default)
        where T : class, IIntegrationEvent
    {
        _logger.LogInformation(
            "Publishing integration event {EventType} with EventId {EventId}",
            typeof(T).Name, @event.EventId);

        await _publishEndpoint.Publish(@event, ct);

        _logger.LogInformation(
            "Published integration event {EventType} with EventId {EventId}",
            typeof(T).Name, @event.EventId);
    }
}
