using Jobsite.Api.Infrastructure;
using Jobsite.SharedKernel.Domain;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Infrastructure;

public sealed class MassTransitEventPublisherTests
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly MassTransitEventPublisher _sut;

    public MassTransitEventPublisherTests()
    {
        _publishEndpoint = Substitute.For<IPublishEndpoint>();
        ILogger<MassTransitEventPublisher> logger = Substitute.For<ILogger<MassTransitEventPublisher>>();
        _sut = new MassTransitEventPublisher(_publishEndpoint, logger);
    }

    [Fact]
    public async Task PublishAsync_CallsPublishEndpoint()
    {
        // Arrange
        TestIntegrationEvent @event = new()
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = "test-correlation"
        };

        // Act
        await _sut.PublishAsync(@event, CancellationToken.None);

        // Assert
        await _publishEndpoint.Received(1).Publish(@event, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_ForwardsCancellationToken()
    {
        // Arrange
        TestIntegrationEvent @event = new()
        {
            EventId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow,
            CorrelationId = "test-correlation"
        };
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        // Act
        await _sut.PublishAsync(@event, token);

        // Assert
        await _publishEndpoint.Received(1).Publish(@event, token);
    }

    public sealed class TestIntegrationEvent : IIntegrationEvent
    {
        public Guid EventId { get; init; }
        public DateTime OccurredAt { get; init; }
        public string CorrelationId { get; init; } = string.Empty;
    }
}
