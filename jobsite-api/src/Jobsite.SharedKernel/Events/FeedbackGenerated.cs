using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published by the AI Service when candidate feedback generation is complete.
/// Consumed by: Screening module (to store candidate feedback).
/// </summary>
public sealed class FeedbackGenerated : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string Feedback { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
