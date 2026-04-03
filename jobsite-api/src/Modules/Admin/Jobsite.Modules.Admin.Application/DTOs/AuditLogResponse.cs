namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>Response body for audit log entries.</summary>
public sealed class AuditLogResponse
{
    public required Guid Id { get; init; }
    public required Guid ActorId { get; init; }
    public required string ActorEmail { get; init; }
    public required string ActorRole { get; init; }
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public object? Details { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public required DateTime PerformedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}
