using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// Generates valid JWT tokens for endpoint integration tests.
/// Uses the same secret, issuer, and audience configured in <see cref="JobsiteWebApplicationFactory"/>.
/// </summary>
public static class TestJwtHelper
{
    /// <summary>
    /// Creates a signed JWT access token with the specified claims.
    /// </summary>
    public static string GenerateToken(
        Guid userId,
        Guid tenantId,
        string role = "Applicant",
        string email = "test@testcorp.com",
        int expiresInMinutes = 60)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(JobsiteWebApplicationFactory.TestJwtSecret));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims = new()
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        JwtSecurityToken token = new(
            issuer: "djobsite-iconnect",
            audience: "djobsite-iconnect",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates an expired JWT for testing token expiration handling.
    /// </summary>
    public static string GenerateExpiredToken(Guid userId, Guid tenantId, string role = "Applicant")
    {
        return GenerateToken(userId, tenantId, role, expiresInMinutes: -1);
    }
}
