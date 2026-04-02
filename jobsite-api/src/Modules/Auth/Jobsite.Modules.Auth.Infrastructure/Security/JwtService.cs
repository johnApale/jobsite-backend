using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// JWT access token generation and refresh token utilities.
/// Uses HS256 signing matching the existing JWT bearer configuration.
/// </summary>
public sealed class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(JwtSettings settings)
    {
        _settings = settings;
    }

    public int AccessTokenExpirationMinutes => _settings.JwtExpirationMinutes;
    public int RefreshTokenExpirationDays => _settings.RefreshTokenExpirationDays;

    public string GenerateAccessToken(User user, Guid tenantId)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_settings.JwtSecret));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", user.Role),
            new("tenant_id", tenantId.ToString()),
        ];

        JwtSecurityToken token = new(
            issuer: _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.JwtExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        byte[] randomBytes = new byte[64];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string HashToken(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
