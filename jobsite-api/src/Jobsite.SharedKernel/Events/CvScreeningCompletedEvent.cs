using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when automated CV screening completes for an application.
/// Consumed by: Matching module (to compute match scores),
///              Recruitment module (to update application status).
/// </summary>
public sealed class CvScreeningCompletedEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid ScreeningResultId { get; init; }
    public required bool PassedScreening { get; init; }
    public required DateTime CompletedAt { get; init; }
}
