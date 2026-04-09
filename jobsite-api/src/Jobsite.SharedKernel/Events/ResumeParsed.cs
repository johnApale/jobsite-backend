using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published by the AI Service when resume parsing is complete.
/// Consumed by: Profiles module (to store AI-parsed content).
/// </summary>
public sealed class ResumeParsed : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ResumeId { get; init; }
    public required string AiParsedContent { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
