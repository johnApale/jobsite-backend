using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.CrossModule;

/// <summary>
/// Provides aggregate recruitment statistics to the Admin module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class RecruitmentStatsReader : IRecruitmentStatsReader
{
    private readonly RecruitmentDbContext _db;

    public RecruitmentStatsReader(RecruitmentDbContext db) => _db = db;

    public async Task<RecruitmentStatsSnapshot> GetStatsAsync(CancellationToken ct = default)
    {
        int totalJobPostings = await _db.JobPostings.AsNoTracking().CountAsync(ct);
        int activeJobPostings = await _db.JobPostings.AsNoTracking()
            .CountAsync(j => j.Status == JobPostingStatus.Published, ct);
        int closedJobPostings = await _db.JobPostings.AsNoTracking()
            .CountAsync(j => j.Status == JobPostingStatus.Closed, ct);

        int totalApplications = await _db.Applications.AsNoTracking().CountAsync(ct);
        int submittedApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Submitted, ct);
        int screeningApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Screening, ct);
        int shortlistedApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Shortlisted, ct);
        int rejectedApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Rejected, ct);
        int hiredApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Hired, ct);
        int withdrawnApplications = await _db.Applications.AsNoTracking()
            .CountAsync(a => a.Status == ApplicationStatus.Withdrawn, ct);

        return new RecruitmentStatsSnapshot
        {
            TotalJobPostings = totalJobPostings,
            ActiveJobPostings = activeJobPostings,
            ClosedJobPostings = closedJobPostings,
            TotalApplications = totalApplications,
            SubmittedApplications = submittedApplications,
            ScreeningApplications = screeningApplications,
            ShortlistedApplications = shortlistedApplications,
            RejectedApplications = rejectedApplications,
            HiredApplications = hiredApplications,
            WithdrawnApplications = withdrawnApplications
        };
    }
}
