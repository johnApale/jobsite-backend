using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when a resume is uploaded and needs parsing.
/// Consumed by: Profiles module (ResumeUploadedConsumer) for async text extraction.
/// </summary>
public sealed class ResumeUploadedEvent : IDomainEvent, IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid ResumeId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid TenantId { get; init; }
    public required string FileUrl { get; init; }
    public required string FileType { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
