using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.CrossModule;

/// <summary>
/// Provides screening question data to the Screening module
/// without requiring a cross-module project reference.
/// </summary>
public sealed class JobScreeningQuestionsReader : IJobScreeningQuestionsReader
{
    private readonly RecruitmentDbContext _db;

    public JobScreeningQuestionsReader(RecruitmentDbContext db) => _db = db;

    public async Task<List<QuestionSnapshot>> GetQuestionsForJobAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        List<QuestionSnapshot> questions = await _db.JobScreeningQuestions
            .AsNoTracking()
            .Where(q => q.JobPostingId == jobPostingId)
            .OrderBy(q => q.DisplayOrder)
            .Select(q => new QuestionSnapshot
            {
                Id = q.Id,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                Timing = q.Timing,
                IsRequired = q.IsRequired,
                Weight = q.Weight,
                ExpectedAnswer = q.ExpectedAnswer,
                Options = q.Options
            })
            .ToListAsync(ct);

        return questions;
    }

    public async Task<bool> HasAfterScreeningQuestionsAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.JobScreeningQuestions
            .AsNoTracking()
            .AnyAsync(q => q.JobPostingId == jobPostingId && q.Timing == "AfterScreening", ct);
    }
}
