using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Published to the message broker when an application needs AI screening evaluation.
/// Consumed by: AI Service.
/// </summary>
public sealed class ScreeningEvaluationRequested : IIntegrationEvent
{
    public required Guid EventId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required string CriteriaJson { get; init; }
    public required string ApplicantDataJson { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
}
