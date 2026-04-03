using System.Text.Json;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Admin.Infrastructure.Repositories;

/// <summary>
/// Repository for append-only <c>admin.audit_logs</c>.
/// Supports cursor-based pagination using (performed_at, id) as the composite cursor.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AdminDbContext _db;

    public AuditLogRepository(AdminDbContext db) => _db = db;

    public async Task<AuditLogPageResponse> GetPageAsync(
        AuditLogQueryParameters parameters, CancellationToken ct = default)
    {
        int pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        IQueryable<AuditLog> query = _db.AuditLogs.AsNoTracking();

        // Apply filters
        if (parameters.Action is not null)
            query = query.Where(al => al.Action == parameters.Action);

        if (parameters.ActorId is not null)
            query = query.Where(al => al.ActorId == parameters.ActorId);

        if (parameters.EntityType is not null)
            query = query.Where(al => al.EntityType == parameters.EntityType);

        if (parameters.DateFrom is not null)
            query = query.Where(al => al.PerformedAt >= parameters.DateFrom.Value);

        if (parameters.DateTo is not null)
            query = query.Where(al => al.PerformedAt <= parameters.DateTo.Value);

        // Apply cursor (performed_at DESC, id DESC)
        if (parameters.Cursor is not null)
        {
            (DateTime cursorDate, Guid cursorId) = DecodeCursor(parameters.Cursor);
            query = query.Where(al =>
                al.PerformedAt < cursorDate ||
                (al.PerformedAt == cursorDate && al.Id.CompareTo(cursorId) < 0));
        }

        // Order by performed_at DESC, id DESC for stable cursor pagination
        List<AuditLog> items = await query
            .OrderByDescending(al => al.PerformedAt)
            .ThenByDescending(al => al.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].PerformedAt, items[^1].Id)
            : null;

        List<AuditLogResponse> responses = items.Select(al => new AuditLogResponse
        {
            Id = al.Id,
            ActorId = al.ActorId,
            ActorEmail = al.ActorEmail,
            ActorRole = al.ActorRole,
            Action = al.Action,
            EntityType = al.EntityType,
            EntityId = al.EntityId,
            Details = al.Details is not null ? JsonSerializer.Deserialize<object>(al.Details) : null,
            IpAddress = al.IpAddress,
            UserAgent = al.UserAgent,
            PerformedAt = al.PerformedAt,
            CreatedAt = al.CreatedAt
        }).ToList();

        return new AuditLogPageResponse
        {
            Items = responses,
            NextCursor = nextCursor
        };
    }

    public void Add(AuditLog log)
    {
        _db.AuditLogs.Add(log);
    }

    private static string EncodeCursor(DateTime performedAt, Guid id)
    {
        string raw = $"{performedAt:O}|{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTime, Guid) DecodeCursor(string cursor)
    {
        string raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        string[] parts = raw.Split('|', 2);
        return (DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind), Guid.Parse(parts[1]));
    }
}
