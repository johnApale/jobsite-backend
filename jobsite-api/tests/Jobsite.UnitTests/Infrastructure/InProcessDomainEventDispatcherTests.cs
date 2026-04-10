using FluentAssertions;
using Jobsite.Api.Infrastructure;
using Jobsite.SharedKernel.Domain;
using NSubstitute;

namespace Jobsite.UnitTests.Infrastructure;

public sealed class InProcessDomainEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_SingleHandler_InvokesHandler()
    {
        // Arrange
        TestHandler handler = new();
        IServiceProvider serviceProvider = CreateServiceProvider<TestEvent>([handler]);
        InProcessDomainEventDispatcher sut = new(serviceProvider, []);
        TestEvent domainEvent = new();

        // Act
        await sut.DispatchAsync(domainEvent, CancellationToken.None);

        // Assert
        handler.ReceivedEvents.Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public async Task DispatchAsync_MultipleHandlers_InvokesAll()
    {
        // Arrange
        TestHandler handler1 = new();
        TestHandler handler2 = new();
        IServiceProvider serviceProvider = CreateServiceProvider<TestEvent>([handler1, handler2]);
        InProcessDomainEventDispatcher sut = new(serviceProvider, []);
        TestEvent domainEvent = new();

        // Act
        await sut.DispatchAsync(domainEvent, CancellationToken.None);

        // Assert
        handler1.ReceivedEvents.Should().ContainSingle();
        handler2.ReceivedEvents.Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchAsync_NoHandlers_CompletesWithoutError()
    {
        // Arrange
        IServiceProvider serviceProvider = CreateServiceProvider<TestEvent>([]);
        InProcessDomainEventDispatcher sut = new(serviceProvider, []);
        TestEvent domainEvent = new();

        // Act
        Func<Task> act = () => sut.DispatchAsync(domainEvent, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_WithBehavior_ExecutesBehaviorAroundHandler()
    {
        // Arrange
        List<string> callOrder = [];
        TrackingBehavior behavior = new(callOrder);
        TrackingHandler handler = new(callOrder);
        IServiceProvider serviceProvider = CreateServiceProvider<TestEvent>([handler]);
        InProcessDomainEventDispatcher sut = new(serviceProvider, [behavior]);
        TestEvent domainEvent = new();

        // Act
        await sut.DispatchAsync(domainEvent, CancellationToken.None);

        // Assert
        callOrder.Should().Equal("behavior-before", "handler", "behavior-after");
    }

    [Fact]
    public async Task DispatchAsync_ForwardsCancellationToken()
    {
        // Arrange
        CancellationTokenCapture handler = new();
        IServiceProvider serviceProvider = CreateServiceProvider<TestEvent>([handler]);
        using CancellationTokenSource cts = new();
        CancellationToken expectedToken = cts.Token;
        InProcessDomainEventDispatcher sut = new(serviceProvider, []);

        // Act
        await sut.DispatchAsync(new TestEvent(), expectedToken);

        // Assert
        handler.CapturedToken.Should().Be(expectedToken);
    }

    // ─── Test helpers ────────────────────────────────────────────────────

    private static IServiceProvider CreateServiceProvider<T>(
        IEnumerable<IDomainEventHandler<T>> handlers) where T : IDomainEvent
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEnumerable<IDomainEventHandler<T>>))
            .Returns(handlers);
        return serviceProvider;
    }

    private sealed class TestEvent : IDomainEvent;

    private sealed class TestHandler : IDomainEventHandler<TestEvent>
    {
        public List<TestEvent> ReceivedEvents { get; } = [];

        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            ReceivedEvents.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingHandler : IDomainEventHandler<TestEvent>
    {
        private readonly List<string> _callOrder;
        public TrackingHandler(List<string> callOrder) => _callOrder = callOrder;

        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            _callOrder.Add("handler");
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingBehavior : IDomainEventPipelineBehavior
    {
        private readonly List<string> _callOrder;
        public TrackingBehavior(List<string> callOrder) => _callOrder = callOrder;

        public async Task HandleAsync(IDomainEvent domainEvent, Func<Task> next, CancellationToken ct)
        {
            _callOrder.Add("behavior-before");
            await next();
            _callOrder.Add("behavior-after");
        }
    }

    private sealed class CancellationTokenCapture : IDomainEventHandler<TestEvent>
    {
        public CancellationToken CapturedToken { get; private set; }

        public Task HandleAsync(TestEvent domainEvent, CancellationToken ct)
        {
            CapturedToken = ct;
            return Task.CompletedTask;
        }
    }
}
