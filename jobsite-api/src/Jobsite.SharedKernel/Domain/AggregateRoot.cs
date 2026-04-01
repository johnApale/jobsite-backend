namespace Jobsite.SharedKernel.Domain;

/// <summary>
/// Base aggregate root that collects domain events for dispatch after persistence.
/// Domain events are raised via <see cref="RaiseDomainEvent"/> and cleared after publishing.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events pending dispatch.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Queue a domain event. Published after <c>SaveChangesAsync</c> succeeds.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Clear all pending domain events. Called by the unit-of-work after publishing.</summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
