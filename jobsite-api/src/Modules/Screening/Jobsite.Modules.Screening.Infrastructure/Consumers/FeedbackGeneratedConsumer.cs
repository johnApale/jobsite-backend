using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.Consumers;

/// <summary>
/// Receives AI-generated candidate feedback and stores it on the ScreeningResult.
/// </summary>
public sealed class FeedbackGeneratedConsumer : IConsumer<FeedbackGenerated>
{
    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory;
    private readonly ILogger<FeedbackGeneratedConsumer> _logger;

    public FeedbackGeneratedConsumer(
        ITenantConnectionResolver connectionResolver,
        ITenantDbContextFactory<ScreeningDbContext> contextFactory,
        ILogger<FeedbackGeneratedConsumer> logger)
    {
        _connectionResolver = connectionResolver;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FeedbackGenerated> context)
    {
        FeedbackGenerated message = context.Message;
        CancellationToken ct = context.CancellationToken;

        _logger.LogInformation(
            "Received AI feedback for ApplicationId {ApplicationId}, TenantId {TenantId}",
            message.ApplicationId, message.TenantId);

        string connectionString = await _connectionResolver.GetConnectionStringAsync(message.TenantId, ct);
        await using ScreeningDbContext db = _contextFactory.CreateDbContext(connectionString);

        ScreeningResult? result = await db.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == message.ApplicationId, ct);

        if (result is null)
        {
            _logger.LogWarning(
                "ScreeningResult for ApplicationId {ApplicationId} not found, skipping feedback update",
                message.ApplicationId);
            return;
        }

        result.CandidateFeedback = message.Feedback;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated ScreeningResult for ApplicationId {ApplicationId} with candidate feedback ({FeedbackLength} chars)",
            message.ApplicationId, message.Feedback.Length);
    }
}
