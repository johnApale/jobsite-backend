using System.Text.Json;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that parses uploaded resumes asynchronously.
/// Runs basic text extraction first, then attempts AI-powered structured parsing.
/// Resolves the tenant database from the event's TenantId.
/// </summary>
public sealed class ResumeUploadedConsumer : IConsumer<ResumeUploadedEvent>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ITenantConnectionResolver _connectionResolver;
    private readonly ITenantDbContextFactory<ProfilesDbContext> _contextFactory;
    private readonly IResumeParser _resumeParser;
    private readonly IAiResumeParser _aiResumeParser;
    private readonly ILogger<ResumeUploadedConsumer> _logger;

    public ResumeUploadedConsumer(
        ITenantConnectionResolver connectionResolver,
        ITenantDbContextFactory<ProfilesDbContext> contextFactory,
        IResumeParser resumeParser,
        IAiResumeParser aiResumeParser,
        ILogger<ResumeUploadedConsumer> logger)
    {
        _connectionResolver = connectionResolver;
        _contextFactory = contextFactory;
        _resumeParser = resumeParser;
        _aiResumeParser = aiResumeParser;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ResumeUploadedEvent> context)
    {
        ResumeUploadedEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        _logger.LogInformation(
            "Processing resume parse for ResumeId {ResumeId}, TenantId {TenantId}",
            message.ResumeId, message.TenantId);

        string connectionString = await _connectionResolver.GetConnectionStringAsync(message.TenantId, ct);
        await using ProfilesDbContext db = _contextFactory.CreateDbContext(connectionString);

        Resume? resume = await db.Resumes.FindAsync([message.ResumeId], ct);

        if (resume is null)
        {
            _logger.LogWarning("Resume {ResumeId} not found, skipping parse", message.ResumeId);
            return;
        }

        if (resume.IsParsed)
        {
            _logger.LogInformation("Resume {ResumeId} already parsed, skipping", message.ResumeId);
            return;
        }

        try
        {
            ResumeParseResult result = await _resumeParser.ParseAsync(message.FileUrl, message.FileType, ct);

            resume.ParsedText = result.ParsedText;
            resume.ExtractedSkills = result.ExtractedSkills;
            resume.IsParsed = true;
            resume.ParsedAt = DateTime.UtcNow;
            resume.ParseError = null;

            // Attempt AI-powered structured extraction (graceful fallback)
            AiResumeParseResult? aiResult = await _aiResumeParser.ParseAsync(result.ParsedText, ct);
            if (aiResult is not null)
            {
                resume.AiParsedContent = JsonSerializer.Serialize(aiResult, JsonOptions);
                _logger.LogInformation(
                    "AI-parsed structured content stored for resume {ResumeId}", message.ResumeId);
            }

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Successfully parsed resume {ResumeId}: {TextLength} chars extracted",
                message.ResumeId, result.ParsedText.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse resume {ResumeId}", message.ResumeId);

            resume.ParseError = ex.Message;
            await db.SaveChangesAsync(ct);

            throw; // Let MassTransit retry policy handle redelivery
        }
    }
}
