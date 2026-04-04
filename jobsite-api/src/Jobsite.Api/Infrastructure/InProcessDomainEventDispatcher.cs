using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Api.Infrastructure;

/// <summary>
/// Dispatches domain events to all registered <see cref="IDomainEventHandler{T}"/> implementations.
/// Wraps handler invocation with registered <see cref="IDomainEventPipelineBehavior"/> middleware.
/// Registered as scoped in DI so it shares the request's service scope.
/// </summary>
public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IDomainEventPipelineBehavior> _behaviors;

    public InProcessDomainEventDispatcher(
        IServiceProvider serviceProvider,
        IEnumerable<IDomainEventPipelineBehavior> behaviors)
    {
        _serviceProvider = serviceProvider;
        _behaviors = behaviors;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        Type eventType = domainEvent.GetType();
        Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        IEnumerable<object?> handlers = _serviceProvider.GetServices(handlerType);

        foreach (object? handler in handlers)
        {
            if (handler is null)
                continue;

            Task InvokeHandler() => InvokeHandlerAsync(handler, handlerType, domainEvent, ct);

            // Build the pipeline: wrap the handler invocation with each behavior (outermost first)
            Func<Task> pipeline = InvokeHandler;
            foreach (IDomainEventPipelineBehavior behavior in _behaviors.Reverse())
            {
                Func<Task> next = pipeline;
                pipeline = () => behavior.HandleAsync(domainEvent, next, ct);
            }

            await pipeline();
        }
    }

    private static Task InvokeHandlerAsync(
        object handler, Type handlerType, IDomainEvent domainEvent, CancellationToken ct)
    {
        System.Reflection.MethodInfo? method = handlerType.GetMethod("HandleAsync");

        if (method is null)
            throw new InvalidOperationException(
                $"Handler {handler.GetType().Name} does not implement HandleAsync.");

        return (Task)method.Invoke(handler, [domainEvent, ct])!;
    }
}
