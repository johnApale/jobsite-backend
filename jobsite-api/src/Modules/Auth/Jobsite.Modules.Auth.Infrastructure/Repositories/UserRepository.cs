using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Auth.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for user lookups against the tenant Auth DB.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AuthDbContext _db;

    public UserRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByEmailForUpdateAsync(string email, CancellationToken ct = default)
    {
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByExternalLoginAsync(string provider, string providerSubjectId, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.ExternalLogins.Any(
                e => e.Provider == provider && e.ProviderSubjectId == providerSubjectId), ct);
    }

    public void Add(User user)
    {
        _db.Users.Add(user);
    }
}
