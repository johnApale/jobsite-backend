using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when the AI Interview Service completes an interview.
/// Consumed by: Screening or Matching module (to incorporate AI interview scores).
/// </summary>
public sealed class InterviewCompletedEvent : IDomainEvent, IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InterviewSessionId { get; init; }
    public required int OverallScore { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
