using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.CrossModule;

/// <summary>
/// Provides job evaluation criteria data to the Screening module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class JobCriteriaReader : IJobCriteriaReader
{
    private readonly RecruitmentDbContext _db;

    public JobCriteriaReader(RecruitmentDbContext db) => _db = db;

    public async Task<List<CriteriaSnapshot>> GetCriteriaForJobAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        List<CriteriaSnapshot> criteria = await _db.JobEvaluationCriteria
            .AsNoTracking()
            .Where(c => c.JobPostingId == jobPostingId)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CriteriaSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Category = c.Category,
                EvaluationMethod = c.EvaluationMethod,
                IsRequired = c.IsRequired,
                Weight = c.Weight,
                Configuration = c.Configuration
            })
            .ToListAsync(ct);

        return criteria;
    }
}
