using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Auth.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for refresh token lookups and management.
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AuthDbContext _db;

    public RefreshTokenRepository(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);
    }

    public async Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default)
    {
        List<RefreshToken> familyTokens = await _db.RefreshTokens
            .Where(r => r.FamilyId == familyId && !r.IsRevoked)
            .ToListAsync(ct);

        foreach (RefreshToken token in familyTokens)
        {
            token.Revoke();
        }
    }

    public void Add(RefreshToken refreshToken)
    {
        _db.RefreshTokens.Add(refreshToken);
    }
}
