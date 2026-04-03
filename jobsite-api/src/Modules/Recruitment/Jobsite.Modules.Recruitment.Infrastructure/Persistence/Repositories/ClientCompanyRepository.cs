using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for client company lookups against the tenant Recruitment DB.
/// Supports cursor-based pagination using (created_at DESC, id DESC).
/// </summary>
public sealed class ClientCompanyRepository : IClientCompanyRepository
{
    private readonly RecruitmentDbContext _db;

    public ClientCompanyRepository(RecruitmentDbContext db)
    {
        _db = db;
    }

    public async Task<ClientCompany?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ClientCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<ClientCompany?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ClientCompanies
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<ClientCompanyListResponse> ListAsync(
        ClientCompanyQueryParameters parameters, CancellationToken ct = default)
    {
        int pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        IQueryable<ClientCompany> query = _db.ClientCompanies.AsNoTracking();

        if (parameters.Status is not null)
            query = query.Where(c => c.Status == parameters.Status);

        if (parameters.Cursor is not null)
        {
            (DateTime cursorDate, Guid cursorId) = DecodeCursor(parameters.Cursor);
            query = query.Where(c =>
                c.CreatedAt < cursorDate ||
                (c.CreatedAt == cursorDate && c.Id.CompareTo(cursorId) < 0));
        }

        List<ClientCompany> items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        bool hasMore = items.Count > pageSize;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        string? nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1].CreatedAt, items[^1].Id)
            : null;

        List<ClientCompanyResponse> responses = items.ConvertAll(c => new ClientCompanyResponse
        {
            Id = c.Id,
            Name = c.Name,
            DisplayName = c.DisplayName,
            IsAnonymous = c.IsAnonymous,
            Industry = c.Industry,
            Website = c.Website,
            ContactName = c.ContactName,
            ContactEmail = c.ContactEmail,
            ContactPhone = c.ContactPhone,
            Notes = c.Notes,
            Status = c.Status,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        });

        return new ClientCompanyListResponse
        {
            Items = responses,
            NextCursor = nextCursor
        };
    }

    public async Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ClientCompanies.AnyAsync(c => c.Id == id, ct);
    }

    public void Add(ClientCompany clientCompany)
    {
        _db.ClientCompanies.Add(clientCompany);
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
