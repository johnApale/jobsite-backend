using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for application lookups against the tenant Recruitment DB.
/// Supports cursor-based pagination using (submitted_at DESC, id DESC).
/// </summary>
public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly RecruitmentDbContext _db;

    public ApplicationRepository(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<ApplicationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<ApplicationEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<ApplicationListResponse> ListAsync(
        ApplicationQueryParameters parameters, CancellationToken ct = default)
    {
        int pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        IQueryable<ApplicationEntity> query = _db.Applications.AsNoTracking();

        if (parameters.JobPostingId is not null)
            query = query.Where(a => a.JobPostingId == parameters.JobPostingId);

        if (parameters.Status is not null)
            query = query.Where(a => a.Status == parameters.Status);

        if (parameters.ApplicantId is not null)
            query = query.Where(a => a.ApplicantId == parameters.ApplicantId);

        if (parameters.Cursor is not null)
        {
            (DateTime cursorDate, Guid cursorId) = DecodeCursor(parameters.Cursor);
            query = query.Where(a =>
                a.SubmittedAt < cursorDate ||
                (a.SubmittedAt == cursorDate && a.Id.CompareTo(cursorId) < 0));
        }

        List<ApplicationEntity> items = await query
            .OrderByDescending(a => a.SubmittedAt)
            .ThenByDescending(a => a.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].SubmittedAt, items[^1].Id)
            : null;

        List<ApplicationResponse> responses = items.ConvertAll(a => new ApplicationResponse
        {
            Id = a.Id,
            JobPostingId = a.JobPostingId,
            ApplicantId = a.ApplicantId,
            Status = a.Status,
            ResumeId = a.ResumeId,
            CoverLetterUrl = a.CoverLetterUrl,
            RejectionReason = a.RejectionReason,
            RejectedAtStage = a.RejectedAtStage,
            WithdrawnAt = a.WithdrawnAt,
            SubmittedAt = a.SubmittedAt,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        });

        return new ApplicationListResponse
        {
            Items = responses,
            NextCursor = nextCursor
        };
    }

    public async Task<bool> ExistsByApplicantAndJobAsync(
        Guid applicantId, Guid jobPostingId, CancellationToken ct = default)
    {
        return await _db.Applications
            .AnyAsync(a => a.ApplicantId == applicantId && a.JobPostingId == jobPostingId, ct);
    }

    public void Add(ApplicationEntity application)
    {
        _db.Applications.Add(application);
    }

    private static string EncodeCursor(DateTime submittedAt, Guid id)
    {
        string raw = $"{submittedAt:O}|{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTime, Guid) DecodeCursor(string cursor)
    {
        string raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        string[] parts = raw.Split('|', 2);
        return (DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind), Guid.Parse(parts[1]));
    }
}
