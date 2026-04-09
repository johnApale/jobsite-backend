using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when a resume needs AI-powered parsing.
/// Consumed by: AI Service.
/// </summary>
public sealed class ResumeParseRequested : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ResumeId { get; init; }
    public required string ParsedText { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
