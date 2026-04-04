using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when an application is submitted.
/// </summary>
public sealed class ApplicationSubmittedAuditHandler : IDomainEventHandler<ApplicationSubmittedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public ApplicationSubmittedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(ApplicationSubmittedEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: domainEvent.ApplicantUserId,
            actorEmail: "system",
            actorRole: "Applicant",
            action: AuditAction.ApplicationSubmitted,
            entityType: AuditEntityType.Application,
            entityId: domainEvent.ApplicationId,
            details: new
            {
                job_posting_id = domainEvent.JobPostingId,
                submitted_at = domainEvent.SubmittedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}
