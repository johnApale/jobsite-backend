using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a candidate is shortlisted.
/// </summary>
public sealed class CandidateShortlistedAuditHandler : INotificationHandler<CandidateShortlistedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public CandidateShortlistedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(CandidateShortlistedEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.CandidateShortlisted,
            entityType: AuditEntityType.Application,
            entityId: notification.ApplicationId,
            details: new
            {
                job_posting_id = notification.JobPostingId,
                applicant_user_id = notification.ApplicantUserId,
                shortlisted_at = notification.ShortlistedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}
