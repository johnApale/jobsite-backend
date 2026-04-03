using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Admin.Domain.Entities;

/// <summary>
/// Append-only audit trail entry. Rows are never updated or deleted.
/// Actor data is denormalized to survive user deletion.
/// Maps to <c>admin.audit_logs</c>.
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>The user who performed the action. Plain UUID, not a FK — survives user deletion.</summary>
    public Guid ActorId { get; set; }

    /// <summary>Denormalized email at time of action.</summary>
    public string ActorEmail { get; set; } = null!;

    /// <summary>Denormalized role at time of action.</summary>
    public string ActorRole { get; set; } = null!;

    /// <summary>What was done (e.g., UserRegistered, SettingsUpdated).</summary>
    public string Action { get; set; } = null!;

    /// <summary>What type of entity was affected (e.g., User, CompanySettings).</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>The ID of the affected entity. NULL for bulk/system operations.</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Structured context about the action (JSONB). Contents vary by action type.</summary>
    public string? Details { get; set; }

    /// <summary>Client IP address at time of action. IPv4/IPv6.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Client user agent string.</summary>
    public string? UserAgent { get; set; }

    /// <summary>When the action occurred.</summary>
    public DateTime PerformedAt { get; set; }
}
