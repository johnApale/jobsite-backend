using FluentAssertions;
using Jobsite.Api.Behaviors;
using Jobsite.SharedKernel.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Behaviors;

public sealed class LoggingEventBehaviorTests
{
    private readonly ILogger<LoggingEventBehavior> _logger = Substitute.For<ILogger<LoggingEventBehavior>>();
    private readonly LoggingEventBehavior _sut;

    public LoggingEventBehaviorTests()
    {
        _sut = new LoggingEventBehavior(_logger);
    }

    [Fact]
    public async Task HandleAsync_LogsStartAndCompletion()
    {
        // Arrange
        TestEvent domainEvent = new();
        bool nextCalled = false;
        Func<Task> next = () => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await _sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
        _logger.ReceivedWithAnyArgs(2).Log(
            default, default, default!, default!, default!);
    }

    [Fact]
    public async Task HandleAsync_CallsNext()
    {
        // Arrange
        TestEvent domainEvent = new();
        bool nextCalled = false;
        Func<Task> next = () => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await _sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenNextThrows_PropagatesException()
    {
        // Arrange
        TestEvent domainEvent = new();
        Func<Task> next = () => throw new InvalidOperationException("boom");

        // Act
        Func<Task> act = async () => await _sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    private sealed class TestEvent : IDomainEvent;
}
