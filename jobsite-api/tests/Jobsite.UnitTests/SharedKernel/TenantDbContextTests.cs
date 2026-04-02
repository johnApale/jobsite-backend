using FluentAssertions;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Jobsite.UnitTests.SharedKernel;

public sealed class TenantDbContextTests
{
    private sealed class TestAggregate : AggregateRoot
    {
        public string Name { get; set; } = null!;

        public void DoSomething()
        {
            RaiseDomainEvent(new TestDomainEvent { AggregateId = Id });
        }
    }

    private sealed class TestDomainEvent : IDomainEvent
    {
        public required Guid AggregateId { get; init; }
    }

    private sealed class TestTenantDbContext : TenantDbContext
    {
        public DbSet<TestAggregate> Aggregates => Set<TestAggregate>();

        public TestTenantDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
            : base(options, dispatcher)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestAggregate>(builder =>
            {
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Name).HasMaxLength(100);
            });
        }
    }

    private static TestTenantDbContext CreateContext(IDomainEventDispatcher? dispatcher = null)
    {
        DbContextOptions<TestTenantDbContext> options = new DbContextOptionsBuilder<TestTenantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new TestTenantDbContext(options, dispatcher);
    }

    [Fact]
    public async Task SaveChangesAsync_AggregateWithEvents_DispatchesEventsAfterSave()
    {
        // Arrange
        IDomainEventDispatcher dispatcher = Substitute.For<IDomainEventDispatcher>();
        TestTenantDbContext context = CreateContext(dispatcher);
        TestAggregate aggregate = new() { Id = Guid.NewGuid(), Name = "Test" };
        aggregate.DoSomething();
        context.Aggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        await dispatcher.Received(1).DispatchAsync(
            Arg.Is<TestDomainEvent>(e => e.AggregateId == aggregate.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_AggregateWithEvents_ClearsEventsAfterDispatch()
    {
        // Arrange
        IDomainEventDispatcher dispatcher = Substitute.For<IDomainEventDispatcher>();
        TestTenantDbContext context = CreateContext(dispatcher);
        TestAggregate aggregate = new() { Id = Guid.NewGuid(), Name = "Test" };
        aggregate.DoSomething();
        aggregate.DoSomething(); // Two events
        context.Aggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_NoDispatcher_SavesWithoutError()
    {
        // Arrange
        TestTenantDbContext context = CreateContext(dispatcher: null);
        TestAggregate aggregate = new() { Id = Guid.NewGuid(), Name = "Test" };
        aggregate.DoSomething();
        context.Aggregates.Add(aggregate);

        // Act
        int result = await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_MultipleAggregatesWithEvents_DispatchesAllEvents()
    {
        // Arrange
        IDomainEventDispatcher dispatcher = Substitute.For<IDomainEventDispatcher>();
        TestTenantDbContext context = CreateContext(dispatcher);

        TestAggregate aggregate1 = new() { Id = Guid.NewGuid(), Name = "First" };
        TestAggregate aggregate2 = new() { Id = Guid.NewGuid(), Name = "Second" };
        aggregate1.DoSomething();
        aggregate2.DoSomething();
        context.Aggregates.Add(aggregate1);
        context.Aggregates.Add(aggregate2);

        // Act
        await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        await dispatcher.Received(2).DispatchAsync(
            Arg.Any<TestDomainEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_NoEvents_DoesNotCallDispatcher()
    {
        // Arrange
        IDomainEventDispatcher dispatcher = Substitute.For<IDomainEventDispatcher>();
        TestTenantDbContext context = CreateContext(dispatcher);
        TestAggregate aggregate = new() { Id = Guid.NewGuid(), Name = "Test" };
        context.Aggregates.Add(aggregate);

        // Act
        await context.SaveChangesAsync(CancellationToken.None);

        // Assert
        await dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IDomainEvent>(),
            Arg.Any<CancellationToken>());
    }
}
