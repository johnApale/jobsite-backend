using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published by the AI Service when free-text answer scoring is complete.
/// Consumed by: Screening module (to store answer scores).
/// </summary>
public sealed class AnswersScored : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string ScoresJson { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
