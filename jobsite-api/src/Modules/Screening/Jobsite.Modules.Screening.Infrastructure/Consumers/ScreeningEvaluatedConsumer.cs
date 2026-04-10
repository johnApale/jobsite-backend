using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.Consumers;

/// <summary>
/// Receives AI screening evaluation results and updates the ScreeningResult with AI-specific fields.
/// </summary>
public sealed class ScreeningEvaluatedConsumer : IConsumer<ScreeningEvaluated>
{
    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory;
    private readonly ILogger<ScreeningEvaluatedConsumer> _logger;

    public ScreeningEvaluatedConsumer(
        ITenantConnectionResolver connectionResolver,
        ITenantDbContextFactory<ScreeningDbContext> contextFactory,
        ILogger<ScreeningEvaluatedConsumer> logger)
    {
        _connectionResolver = connectionResolver;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ScreeningEvaluated> context)
    {
        ScreeningEvaluated message = context.Message;
        CancellationToken ct = context.CancellationToken;

        _logger.LogInformation(
            "Received AI screening evaluation for ApplicationId {ApplicationId}, TenantId {TenantId}",
            message.ApplicationId, message.TenantId);

        string connectionString = await _connectionResolver.GetConnectionStringAsync(message.TenantId, ct);
        await using ScreeningDbContext db = _contextFactory.CreateDbContext(connectionString);

        ScreeningResult? result = await db.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == message.ApplicationId, ct);

        if (result is null)
        {
            _logger.LogWarning(
                "ScreeningResult for ApplicationId {ApplicationId} not found, skipping AI evaluation update",
                message.ApplicationId);
            return;
        }

        result.AiCriteriaScoreBreakdown = message.BreakdownJson;
        result.AiOverallScore = message.OverallScore;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated ScreeningResult for ApplicationId {ApplicationId} with AI score {AiScore}",
            message.ApplicationId, message.OverallScore);
    }
}
