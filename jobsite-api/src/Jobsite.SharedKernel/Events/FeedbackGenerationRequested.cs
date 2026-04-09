using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when candidate feedback needs to be generated.
/// Consumed by: AI Service.
/// </summary>
public sealed class FeedbackGenerationRequested : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string CriteriaBreakdown { get; init; }
    public required decimal OverallScore { get; init; }
    public required string TransparencyLevel { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
