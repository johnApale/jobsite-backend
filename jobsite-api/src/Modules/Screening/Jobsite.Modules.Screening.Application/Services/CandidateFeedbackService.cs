using Jobsite.Modules.Screening.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.Services;

public sealed class CandidateFeedbackService
{
    private readonly IAiCandidateFeedbackClient _feedbackClient;
    private readonly ILogger<CandidateFeedbackService> _logger;

    public CandidateFeedbackService(
        IAiCandidateFeedbackClient feedbackClient,
        ILogger<CandidateFeedbackService> logger)
    {
        _feedbackClient = feedbackClient;
        _logger = logger;
    }

    public async Task<string?> GenerateFeedbackAsync(
        string criteriaBreakdown, decimal overallScore, string transparencyLevel,
        CancellationToken ct = default)
    {
        string? feedback = await _feedbackClient.GenerateFeedbackAsync(
            criteriaBreakdown, overallScore, transparencyLevel, ct);

        if (feedback is null)
        {
            _logger.LogWarning("AI candidate feedback generation returned null — transparency unavailable");
        }

        return feedback;
    }
}
