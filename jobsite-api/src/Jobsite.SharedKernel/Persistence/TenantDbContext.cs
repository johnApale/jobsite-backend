using Jobsite.SharedKernel.Domain;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Abstract base DbContext for per-tenant databases.
/// Handles snake_case naming and domain event dispatch after save.
/// Each module creates a concrete DbContext inheriting from this class
/// (e.g., <c>AuthDbContext : TenantDbContext</c>).
/// </summary>
public abstract class TenantDbContext : DbContext, IUnitOfWork
{
    private readonly IDomainEventDispatcher? _dispatcher;

    protected TenantDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher = null)
        : base(options)
    {
        _dispatcher = dispatcher;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// Saves changes, then dispatches all domain events raised by aggregate roots.
    /// Events are cleared after dispatch to prevent duplicate publishing.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        int result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEventsAsync(cancellationToken);
        return result;
    }

    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        if (_dispatcher is null)
            return;

        List<AggregateRoot> aggregates = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        List<IDomainEvent> domainEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .ToList();

        foreach (AggregateRoot aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            await _dispatcher.DispatchAsync(domainEvent, ct);
        }
    }
}
