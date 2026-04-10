using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Screening.Infrastructure.Persistence.Repositories;

public sealed class ScreeningResultRepository : IScreeningResultRepository
{
    private readonly ScreeningDbContext _db;

    public ScreeningResultRepository(ScreeningDbContext db)
    {
        _db = db;
    }

    public async Task<ScreeningResult?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default)
    {
        return await _db.ScreeningResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);
    }

    public async Task<ScreeningResult?> GetByApplicationIdForUpdateAsync(Guid applicationId, CancellationToken ct = default)
    {
        return await _db.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);
    }

    public async Task<ScreeningResultListResponse> ListAsync(
        ScreeningResultQueryParameters parameters, CancellationToken ct = default)
    {
        IQueryable<ScreeningResult> query = _db.ScreeningResults.AsNoTracking();

        if (parameters.JobPostingId.HasValue)
        {
            // Filter by job posting — requires knowledge of which applications belong to which job.
            // This is a cross-module reference. Since we don't have a navigation property,
            // we rely on the caller filtering by applicationIds or add job_posting_id to the query params.
            // For now, this filter is not directly supported without a cross-module query.
        }

        if (!string.IsNullOrEmpty(parameters.Status))
        {
            query = query.Where(r => r.Status == parameters.Status);
        }

        if (!string.IsNullOrEmpty(parameters.MatchStrength))
        {
            query = query.Where(r => r.MatchStrength == parameters.MatchStrength);
        }

        if (!string.IsNullOrEmpty(parameters.Outcome))
        {
            query = query.Where(r => r.Outcome == parameters.Outcome);
        }

        // Cursor-based pagination
        if (!string.IsNullOrEmpty(parameters.Cursor))
        {
            (DateTime cursorDate, Guid cursorId) = DecodeCursor(parameters.Cursor);
            query = query.Where(r =>
                r.CreatedAt < cursorDate ||
                (r.CreatedAt == cursorDate && r.ApplicationId.CompareTo(cursorId) < 0));
        }

        int pageSize = parameters.PageSize > 0 ? parameters.PageSize : 20;

        List<ScreeningResult> results = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.ApplicationId)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        bool hasMore = results.Count > pageSize;
        if (hasMore)
            results.RemoveAt(results.Count - 1);

        string? nextCursor = hasMore && results.Count > 0
            ? EncodeCursor(results[^1].CreatedAt, results[^1].ApplicationId)
            : null;

        List<ScreeningResultResponse> items = results
            .Select(ScreeningService.MapToResponse)
            .ToList();

        return new ScreeningResultListResponse
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }

    public async Task<List<ScreeningResult>> GetPendingForRescoringAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        // Without a navigation property to applications, this requires a cross-module approach.
        // For now, return empty — rescoring is triggered per-application, not in bulk.
        return await Task.FromResult(new List<ScreeningResult>());
    }

    public void Add(ScreeningResult result)
    {
        _db.ScreeningResults.Add(result);
    }

    private static string EncodeCursor(DateTime createdAt, Guid id)
    {
        string raw = $"{createdAt:O}|{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTime, Guid) DecodeCursor(string cursor)
    {
        string raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        string[] parts = raw.Split('|');
        return (DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind),
                Guid.Parse(parts[1]));
    }
}
