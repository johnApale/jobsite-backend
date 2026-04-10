using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.Consumers;

/// <summary>
/// Receives AI-scored free-text answers and updates individual ScreeningQuestionResponse records.
/// </summary>
public sealed class AnswersScoredConsumer : IConsumer<AnswersScored>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory;
    private readonly ILogger<AnswersScoredConsumer> _logger;

    public AnswersScoredConsumer(
        ITenantConnectionResolver connectionResolver,
        ITenantDbContextFactory<ScreeningDbContext> contextFactory,
        ILogger<AnswersScoredConsumer> logger)
    {
        _connectionResolver = connectionResolver;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AnswersScored> context)
    {
        AnswersScored message = context.Message;
        CancellationToken ct = context.CancellationToken;

        _logger.LogInformation(
            "Received AI answer scores for ApplicationId {ApplicationId}, TenantId {TenantId}",
            message.ApplicationId, message.TenantId);

        List<AnswerScore>? scores = JsonSerializer.Deserialize<List<AnswerScore>>(
            message.ScoresJson, JsonOptions);

        if (scores is null or { Count: 0 })
        {
            _logger.LogWarning("Empty or null scores payload for ApplicationId {ApplicationId}", message.ApplicationId);
            return;
        }

        string connectionString = await _connectionResolver.GetConnectionStringAsync(message.TenantId, ct);
        await using ScreeningDbContext db = _contextFactory.CreateDbContext(connectionString);

        List<ScreeningQuestionResponse> responses = await db.ScreeningQuestionResponses
            .Where(r => r.ApplicationId == message.ApplicationId)
            .ToListAsync(ct);

        Dictionary<Guid, ScreeningQuestionResponse> responseMap = responses
            .ToDictionary(r => r.QuestionId);

        int updated = 0;
        foreach (AnswerScore score in scores)
        {
            if (responseMap.TryGetValue(score.QuestionId, out ScreeningQuestionResponse? response))
            {
                response.Score = score.Score;
                response.ScoreResult = score.Result;
                response.ScoreReasoning = score.Reasoning;
                response.ScoredAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                _logger.LogWarning(
                    "QuestionResponse for QuestionId {QuestionId} not found in ApplicationId {ApplicationId}",
                    score.QuestionId, message.ApplicationId);
            }
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated {Count}/{Total} question responses with AI scores for ApplicationId {ApplicationId}",
            updated, scores.Count, message.ApplicationId);
    }
}
