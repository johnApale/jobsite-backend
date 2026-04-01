using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when a candidate is ready for AI interview.
/// Consumed by: AI Interview Service (Python/FastAPI).
/// </summary>
public sealed class CandidateReadyForInterviewEvent : IDomainEvent, IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
