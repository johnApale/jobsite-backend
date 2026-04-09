using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when free-text answers need AI scoring.
/// Consumed by: AI Service.
/// </summary>
public sealed class AnswerScoringRequested : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string AnswersJson { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
