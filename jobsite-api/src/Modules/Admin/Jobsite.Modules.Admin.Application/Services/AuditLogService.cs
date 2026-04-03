using System.Text.Json;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Admin.Application.Services;

/// <summary>
/// Application service for recording and querying audit trail entries.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuditLogService(
        IAuditLogRepository auditLogRepository,
        [FromKeyedServices("admin")] IUnitOfWork unitOfWork)
    {
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task LogAsync(
        Guid actorId,
        string actorEmail,
        string actorRole,
        string action,
        string entityType,
        Guid? entityId,
        object? details,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        AuditLog log = new()
        {
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is not null ? JsonSerializer.Serialize(details, JsonOptions) : null,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            PerformedAt = DateTime.UtcNow
        };

        _auditLogRepository.Add(log);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AuditLogPageResponse> QueryAsync(
        AuditLogQueryParameters parameters, CancellationToken ct = default)
    {
        return await _auditLogRepository.GetPageAsync(parameters, ct);
    }
}
