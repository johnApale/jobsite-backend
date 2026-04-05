using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Infrastructure.CrossModule;

/// <summary>
/// Provides basic application data to the Matching module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class ApplicationDataReader : IApplicationDataReader
{
    private readonly RecruitmentDbContext _db;

    public ApplicationDataReader(RecruitmentDbContext db) => _db = db;

    public async Task<ApplicationDataSnapshot?> GetApplicationDataAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .Where(a => a.Id == applicationId)
            .Select(a => new ApplicationDataSnapshot
            {
                ApplicationId = a.Id,
                JobPostingId = a.JobPostingId,
                ApplicantUserId = a.ApplicantId
            })
            .FirstOrDefaultAsync(ct);
    }
}
