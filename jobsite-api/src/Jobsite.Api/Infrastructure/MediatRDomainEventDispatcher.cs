using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Persistence;
using MediatR;

namespace Jobsite.Api.Infrastructure;

/// <summary>
/// Dispatches domain events via MediatR's <see cref="IPublisher"/>.
/// Registered as scoped in DI so it shares the request's MediatR scope.
/// </summary>
public sealed class MediatRDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    public MediatRDomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        await _publisher.Publish(domainEvent, ct);
    }
}
