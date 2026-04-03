using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for screening question lookups against the tenant Recruitment DB.
/// </summary>
public sealed class ScreeningQuestionRepository : IScreeningQuestionRepository
{
    private readonly RecruitmentDbContext _db;

    public ScreeningQuestionRepository(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<List<JobScreeningQuestion>> GetByJobPostingIdAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.JobScreeningQuestions
            .AsNoTracking()
            .Where(q => q.JobPostingId == jobPostingId)
            .OrderBy(q => q.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<JobScreeningQuestion?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobScreeningQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct);
    }

    public async Task<JobScreeningQuestion?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobScreeningQuestions
            .FirstOrDefaultAsync(q => q.Id == id, ct);
    }

    public void Add(JobScreeningQuestion question)
    {
        _db.JobScreeningQuestions.Add(question);
    }

    public void Remove(JobScreeningQuestion question)
    {
        _db.JobScreeningQuestions.Remove(question);
    }
}
