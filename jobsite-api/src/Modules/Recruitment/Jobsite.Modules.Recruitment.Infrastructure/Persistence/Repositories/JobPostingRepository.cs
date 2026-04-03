using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for job posting lookups against the tenant Recruitment DB.
/// Supports cursor-based pagination using (created_at DESC, id DESC).
/// </summary>
public sealed class JobPostingRepository : IJobPostingRepository
{
    private readonly RecruitmentDbContext _db;

    public JobPostingRepository(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<JobPosting?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<JobPosting?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobPostings
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<JobPosting?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobPostings
            .AsNoTracking()
            .Include(j => j.Criteria.OrderBy(c => c.DisplayOrder))
            .Include(j => j.Questions.OrderBy(q => q.DisplayOrder))
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<JobPostingListResponse> ListAsync(
        JobPostingQueryParameters parameters, CancellationToken ct = default)
    {
        int pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        IQueryable<JobPosting> query = _db.JobPostings.AsNoTracking();

        if (parameters.Status is not null)
            query = query.Where(j => j.Status == parameters.Status);

        if (parameters.ClientCompanyId is not null)
            query = query.Where(j => j.ClientCompanyId == parameters.ClientCompanyId);

        if (parameters.Cursor is not null)
        {
            (DateTime cursorDate, Guid cursorId) = DecodeCursor(parameters.Cursor);
            query = query.Where(j =>
                j.CreatedAt < cursorDate ||
                (j.CreatedAt == cursorDate && j.Id.CompareTo(cursorId) < 0));
        }

        List<JobPosting> items = await query
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].CreatedAt, items[^1].Id)
            : null;

        List<JobPostingResponse> responses = items.ConvertAll(j => new JobPostingResponse
        {
            Id = j.Id,
            ClientCompanyId = j.ClientCompanyId,
            Title = j.Title,
            Description = j.Description,
            LocationType = j.LocationType,
            City = j.City,
            Country = j.Country,
            EmploymentType = j.EmploymentType,
            SalaryMin = j.SalaryMin,
            SalaryMax = j.SalaryMax,
            SalaryCurrency = j.SalaryCurrency,
            Department = j.Department,
            Status = j.Status,
            PostedBy = j.PostedBy,
            PublishedAt = j.PublishedAt,
            ClosesAt = j.ClosesAt,
            ClosedAt = j.ClosedAt,
            CreatedAt = j.CreatedAt,
            UpdatedAt = j.UpdatedAt
        });

        return new JobPostingListResponse
        {
            Items = responses,
            NextCursor = nextCursor
        };
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.JobPostings.AnyAsync(j => j.Id == id, ct);
    }

    public void Add(JobPosting jobPosting)
    {
        _db.JobPostings.Add(jobPosting);
    }

    private static string EncodeCursor(DateTime createdAt, Guid id)
    {
        string raw = $"{createdAt:O}|{id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static (DateTime, Guid) DecodeCursor(string cursor)
    {
        string raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        string[] parts = raw.Split('|', 2);
        return (DateTime.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind), Guid.Parse(parts[1]));
    }
}
