using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Admin.Infrastructure.Repositories;

/// <summary>
/// Repository for the singleton <c>admin.company_settings</c> row.
/// </summary>
public sealed class CompanySettingsRepository : ICompanySettingsRepository
{
    private readonly AdminDbContext _db;

    public CompanySettingsRepository(AdminDbContext db) => _db = db;

    public async Task<CompanySettings?> GetAsync(CancellationToken ct = default)
    {
        return await _db.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CompanySettings?> GetForUpdateAsync(CancellationToken ct = default)
    {
        return await _db.CompanySettings
            .FirstOrDefaultAsync(ct);
    }

    public void Add(CompanySettings settings)
    {
        _db.CompanySettings.Add(settings);
    }
}
