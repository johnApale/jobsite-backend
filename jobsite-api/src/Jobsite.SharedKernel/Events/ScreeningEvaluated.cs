using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published by the AI Service when screening evaluation is complete.
/// Consumed by: Screening module (to store AI score breakdown).
/// </summary>
public sealed class ScreeningEvaluated : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string BreakdownJson { get; init; }
    public required decimal OverallScore { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
