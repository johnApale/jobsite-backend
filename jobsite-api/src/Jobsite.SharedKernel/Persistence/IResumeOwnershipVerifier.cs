namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Verifies that a resume belongs to a specific user.
/// Implemented by the Profiles module to avoid cross-module project references.
/// Consumed by the Recruitment module during application submission.
/// </summary>
public interface IResumeOwnershipVerifier
{
    /// <summary>Returns <c>true</c> if the resume exists and belongs to the specified user.</summary>
    Task<bool> IsOwnedByUserAsync(Guid resumeId, Guid userId, CancellationToken ct = default);
}
