using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when all assessment (AfterScreening) question answers have been scored.
/// Consumed by: Matching module (to compute final pipeline score),
///              Admin module (audit log).
/// </summary>
public sealed class AssessmentCompletedEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required decimal AssessmentScore { get; init; }
    public required DateTime CompletedAt { get; init; }
}
