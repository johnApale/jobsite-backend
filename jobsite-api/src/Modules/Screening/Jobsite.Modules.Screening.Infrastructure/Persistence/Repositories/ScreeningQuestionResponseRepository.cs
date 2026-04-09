using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence.Repositories;

public sealed class ScreeningQuestionResponseRepository : IScreeningQuestionResponseRepository
{
    private readonly ScreeningDbContext _db;

    public ScreeningQuestionResponseRepository(ScreeningDbContext db)
    {
        _db = db;
    }

    public async Task<List<ScreeningQuestionResponse>> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        return await _db.ScreeningQuestionResponses
            .AsNoTracking()
            .Where(r => r.ApplicationId == applicationId)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<List<ScreeningQuestionResponse>> GetByApplicationIdAndTimingAsync(
        Guid applicationId, string timing, CancellationToken ct = default)
    {
        // We don't store timing on the response entity — it's on the question.
        // To filter by timing, we need to join with questions (cross-module).
        // Since we can't do a cross-module join in EF, we return all responses
        // and let the caller filter by matching question IDs.
        // The service layer fetches questions with timing and filters accordingly.
        return await _db.ScreeningQuestionResponses
            .Where(r => r.ApplicationId == applicationId)
            .OrderBy(r => r.SubmittedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsByApplicationAndQuestionAsync(
        Guid applicationId, Guid questionId, CancellationToken ct = default)
    {
        return await _db.ScreeningQuestionResponses
            .AnyAsync(r => r.ApplicationId == applicationId && r.QuestionId == questionId, ct);
    }

    public void Add(ScreeningQuestionResponse response)
    {
        _db.ScreeningQuestionResponses.Add(response);
    }

    public void AddRange(IEnumerable<ScreeningQuestionResponse> responses)
    {
        _db.ScreeningQuestionResponses.AddRange(responses);
    }
}
