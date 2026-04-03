using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>
/// AI-powered screening question suggestion client. Calls the AI Service to generate
/// screening questions from job description and evaluation criteria. Returns null when
/// the AI Service is unavailable (graceful fallback).
/// </summary>
public interface IAiQuestionSuggester
{
    /// <summary>
    /// Suggest screening questions based on job description and existing criteria.
    /// Returns null on failure or service unavailability.
    /// </summary>
    Task<List<AiQuestionSuggestion>?> SuggestAsync(
        string jobDescription, List<CriteriaResponse> criteria, CancellationToken ct = default);
}
