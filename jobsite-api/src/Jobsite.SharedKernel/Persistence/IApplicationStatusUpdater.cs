namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Updates application status in the Recruitment module.
/// Implemented by Recruitment.Infrastructure to avoid cross-module project references.
/// Consumed by the Screening module to advance/reject applications after evaluation.
/// </summary>
public interface IApplicationStatusUpdater
{
    /// <summary>
    /// Updates the status of an application. Optionally sets rejection reason and stage
    /// when the new status is <c>Rejected</c>.
    /// </summary>
    Task UpdateStatusAsync(
        Guid applicationId,
        string newStatus,
        string? rejectionReason,
        string? rejectedAtStage,
        CancellationToken ct = default);
}
