using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a candidate is shortlisted.
/// </summary>
public sealed class CandidateShortlistedAuditHandler : IDomainEventHandler<CandidateShortlistedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public CandidateShortlistedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(CandidateShortlistedEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.CandidateShortlisted,
            entityType: AuditEntityType.Application,
            entityId: domainEvent.ApplicationId,
            details: new
            {
                job_posting_id = domainEvent.JobPostingId,
                applicant_user_id = domainEvent.ApplicantUserId,
                shortlisted_at = domainEvent.ShortlistedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}
