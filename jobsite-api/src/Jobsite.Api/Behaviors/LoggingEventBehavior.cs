using System.Diagnostics;
using Jobsite.SharedKernel.Domain;

namespace Jobsite.Api.Behaviors;

/// <summary>
/// Domain event pipeline behavior that logs event dispatch start, completion, and elapsed time.
/// </summary>
public sealed class LoggingEventBehavior : IDomainEventPipelineBehavior
{
    private readonly ILogger<LoggingEventBehavior> _logger;

    public LoggingEventBehavior(ILogger<LoggingEventBehavior> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(IDomainEvent domainEvent, Func<Task> next, CancellationToken ct)
    {
        string eventName = domainEvent.GetType().Name;

        _logger.LogInformation("Handling {EventName}", eventName);

        Stopwatch stopwatch = Stopwatch.StartNew();
        await next();
        stopwatch.Stop();

        _logger.LogInformation(
            "Handled {EventName} in {ElapsedMs}ms",
            eventName, stopwatch.ElapsedMilliseconds);
    }
}
