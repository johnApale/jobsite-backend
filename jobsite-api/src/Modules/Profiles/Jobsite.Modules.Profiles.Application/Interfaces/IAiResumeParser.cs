using Jobsite.Modules.Profiles.Application.DTOs;

namespace Jobsite.Modules.Profiles.Application.Interfaces;

/// <summary>
/// AI-powered resume parser that extracts structured data (skills, experience, education)
/// from pre-extracted plain text. Returns null when the AI Service is unavailable.
/// </summary>
public interface IAiResumeParser
{
    /// <summary>
    /// Send extracted text to the AI Service for structured parsing.
    /// Returns null on failure or service unavailability (graceful fallback).
    /// </summary>
    Task<AiResumeParseResult?> ParseAsync(string parsedText, CancellationToken ct = default);
}
