using FluentAssertions;
using Jobsite.SharedKernel.Domain;

namespace Jobsite.UnitTests.SharedKernel;

/// <summary>Tests for AggregateRoot domain event tracking.</summary>
public sealed class AggregateRootTests
{
    private sealed class TestAggregate : AggregateRoot
    {
        public void DoSomething()
        {
            RaiseDomainEvent(new TestEvent());
        }
    }

    private sealed class TestEvent : IDomainEvent;

    [Fact]
    public void RaiseDomainEvent_SingleEvent_AppearsInDomainEvents()
    {
        // Arrange
        TestAggregate aggregate = new();

        // Act
        aggregate.DoSomething();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(1);
        aggregate.DomainEvents[0].Should().BeOfType<TestEvent>();
    }

    [Fact]
    public void RaiseDomainEvent_MultipleEvents_TracksAll()
    {
        // Arrange
        TestAggregate aggregate = new();

        // Act
        aggregate.DoSomething();
        aggregate.DoSomething();
        aggregate.DoSomething();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(3);
    }

    [Fact]
    public void ClearDomainEvents_AfterRaising_RemovesAll()
    {
        // Arrange
        TestAggregate aggregate = new();
        aggregate.DoSomething();
        aggregate.DoSomething();

        // Act
        aggregate.ClearDomainEvents();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_NewAggregate_IsEmpty()
    {
        // Arrange & Act
        TestAggregate aggregate = new();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }
}
