using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>
/// AI scoring client — calls the AI Service for richer per-criterion analysis.
/// Returns null when AI Service is unavailable or returns an error.
/// Feature-flagged: requires both system gate and tenant opt-in.
/// </summary>
public interface IAiScoringClient
{
    /// <summary>
    /// Calls the AI Service to evaluate the applicant against criteria.
    /// Returns null on failure (graceful degradation).
    /// </summary>
    Task<AiScoringResult?> EvaluateAsync(
        List<CriteriaSnapshot> criteria,
        ApplicantDataSnapshot applicantData,
        CancellationToken ct = default);
}
