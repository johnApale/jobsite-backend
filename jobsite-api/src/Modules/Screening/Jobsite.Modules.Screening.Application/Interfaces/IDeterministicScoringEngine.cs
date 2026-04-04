using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>
/// Deterministic scoring engine — evaluates applicant data against job criteria
/// using rule-based logic (ExactMatch, RangeMatch, keyword-based SemanticSimilarity).
/// Always runs, zero-cost, drives all routing decisions.
/// </summary>
public interface IDeterministicScoringEngine
{
    /// <summary>
    /// Scores the applicant against all criteria and returns a weighted breakdown
    /// with an overall score.
    /// </summary>
    Task<ScoringResult> ScoreAsync(
        List<CriteriaSnapshot> criteria,
        ApplicantDataSnapshot applicantData,
        CancellationToken ct = default);
}
