using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Infrastructure.CrossModule;

/// <summary>
/// Allows the Screening module to update application status
/// without requiring a cross-module project reference.
/// </summary>
public sealed class ApplicationStatusUpdater : IApplicationStatusUpdater
{
    private readonly RecruitmentDbContext _db;

    public ApplicationStatusUpdater(RecruitmentDbContext db) => _db = db;

    public async Task UpdateStatusAsync(
        Guid applicationId,
        string newStatus,
        string? rejectionReason,
        string? rejectedAtStage,
        CancellationToken ct = default)
    {
        ApplicationEntity? application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == applicationId, ct);

        if (application is null)
            throw AppErrors.ApplicationNotFound;

        application.Status = newStatus;
        application.RejectionReason = rejectionReason;
        application.RejectedAtStage = rejectedAtStage;
        application.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
