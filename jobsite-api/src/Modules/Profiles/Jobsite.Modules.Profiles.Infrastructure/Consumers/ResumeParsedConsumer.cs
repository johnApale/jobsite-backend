using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Consumers;

/// <summary>
/// Receives AI-parsed resume content from the AI Service and stores it on the Resume entity.
/// </summary>
public sealed class ResumeParsedConsumer : IConsumer<ResumeParsed>
{
    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly ITenantDbContextFactory<ProfilesDbContext> _contextFactory;
    private readonly ILogger<ResumeParsedConsumer> _logger;

    public ResumeParsedConsumer(
        ITenantConnectionResolver connectionResolver,
        ITenantDbContextFactory<ProfilesDbContext> contextFactory,
        ILogger<ResumeParsedConsumer> logger)
    {
        _connectionResolver = connectionResolver;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ResumeParsed> context)
    {
        ResumeParsed message = context.Message;
        CancellationToken ct = context.CancellationToken;

        _logger.LogInformation(
            "Received AI-parsed content for ResumeId {ResumeId}, TenantId {TenantId}",
            message.ResumeId, message.TenantId);

        string connectionString = await _connectionResolver.GetConnectionStringAsync(message.TenantId, ct);
        await using ProfilesDbContext db = _contextFactory.CreateDbContext(connectionString);

        Resume? resume = await db.Resumes.FindAsync([message.ResumeId], ct);

        if (resume is null)
        {
            _logger.LogWarning("Resume {ResumeId} not found, skipping AI content update", message.ResumeId);
            return;
        }

        resume.AiParsedContent = message.AiParsedContent;

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Updated Resume {ResumeId} with AI-parsed content ({ContentLength} chars)",
            message.ResumeId, message.AiParsedContent.Length);
    }
}
