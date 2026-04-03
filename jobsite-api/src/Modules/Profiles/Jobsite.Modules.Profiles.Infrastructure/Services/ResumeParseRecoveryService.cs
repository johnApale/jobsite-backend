using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Services;

/// <summary>
/// On startup, re-publishes <see cref="ResumeUploadedEvent"/> for resumes
/// that were not parsed and have no parse error (interrupted during previous run).
/// </summary>
public sealed class ResumeParseRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResumeParseRecoveryService> _logger;

    public ResumeParseRecoveryService(
        IServiceScopeFactory scopeFactory,
        ILogger<ResumeParseRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let MassTransit/RabbitMQ connection stabilize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IServiceProvider sp = scope.ServiceProvider;

            ITenantConnectionResolver connectionResolver = sp.GetRequiredService<ITenantConnectionResolver>();
            IEventPublisher eventPublisher = sp.GetRequiredService<IEventPublisher>();
            ITenantDbContextFactory<ProfilesDbContext> contextFactory =
                sp.GetRequiredService<ITenantDbContextFactory<ProfilesDbContext>>();

            List<TenantConnection> tenants = await connectionResolver.GetAllConnectionsAsync(stoppingToken);
            int totalRecovered = 0;

            foreach (TenantConnection tenant in tenants)
            {
                stoppingToken.ThrowIfCancellationRequested();

                try
                {
                    await using ProfilesDbContext db = contextFactory.CreateDbContext(tenant.ConnectionString);

                    List<Resume> unparsedResumes = await db.Resumes
                        .AsNoTracking()
                        .Where(r => !r.IsParsed && r.ParseError == null)
                        .ToListAsync(stoppingToken);

                    foreach (Resume resume in unparsedResumes)
                    {
                        await eventPublisher.PublishAsync(new ResumeUploadedEvent
                        {
                            EventId = Guid.NewGuid(),
                            ResumeId = resume.Id,
                            UserId = resume.UserId,
                            TenantId = tenant.TenantId,
                            FileUrl = resume.FileUrl,
                            FileType = resume.FileType,
                            CorrelationId = Guid.NewGuid().ToString(),
                            OccurredAt = DateTime.UtcNow
                        }, stoppingToken);

                        totalRecovered++;
                    }

                    if (unparsedResumes.Count > 0)
                    {
                        _logger.LogInformation(
                            "Re-queued {Count} unparsed resumes for tenant {TenantId}",
                            unparsedResumes.Count, tenant.TenantId);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "Failed to recover unparsed resumes for tenant {TenantId}", tenant.TenantId);
                }
            }

            if (totalRecovered > 0)
            {
                _logger.LogInformation(
                    "Resume parse recovery complete: {Count} resumes re-queued", totalRecovered);
            }
        }
        catch (OperationCanceledException)
        {
            // App shutting down — expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume parse recovery service failed");
        }
    }
}
