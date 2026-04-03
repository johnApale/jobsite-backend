using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>
/// AI-powered criteria suggestion client. Calls the AI Service to generate
/// evaluation criteria from job posting details. Returns null when the AI Service
/// is unavailable (graceful fallback).
/// </summary>
public interface IAiCriteriaSuggester
{
    /// <summary>
    /// Suggest evaluation criteria based on job title and description.
    /// Returns null on failure or service unavailability.
    /// </summary>
    Task<List<AiCriteriaSuggestion>?> SuggestAsync(string jobTitle, string jobDescription, CancellationToken ct = default);
}
