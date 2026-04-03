using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for evaluation criteria lookups against the tenant Recruitment DB.
/// </summary>
public sealed class CriteriaRepository : ICriteriaRepository
{
    private readonly RecruitmentDbContext _db;

    public CriteriaRepository(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<List<JobEvaluationCriteria>> GetByJobPostingIdAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.JobEvaluationCriteria
            .AsNoTracking()
            .Where(c => c.JobPostingId == jobPostingId)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<JobEvaluationCriteria?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobEvaluationCriteria
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<JobEvaluationCriteria?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobEvaluationCriteria
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public void Add(JobEvaluationCriteria criteria)
    {
        _db.JobEvaluationCriteria.Add(criteria);
    }

    public void Remove(JobEvaluationCriteria criteria)
    {
        _db.JobEvaluationCriteria.Remove(criteria);
    }
}
