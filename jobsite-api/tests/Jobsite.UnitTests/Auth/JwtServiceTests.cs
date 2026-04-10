using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Security;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="JwtService"/> token generation and hashing.
/// </summary>
public sealed class JwtServiceTests
{
    private static readonly JwtSettings Settings = new()
    {
        JwtSecret = "ThisIsATestSecretKeyThatIsAtLeast32Characters!",
        JwtIssuer = "test-issuer",
        JwtAudience = "test-audience",
        JwtExpirationMinutes = 30,
        RefreshTokenExpirationDays = 7
    };

    private readonly JwtService _jwtService = new(Settings);

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        // Arrange
        User user = TestData.CreateUser(email: "jwt@test.com", role: UserRole.Recruiter);
        Guid tenantId = Guid.NewGuid();

        // Act
        string token = _jwtService.GenerateAccessToken(user, tenantId);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
    }

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        // Arrange
        User user = TestData.CreateUser(email: "claims@test.com", role: UserRole.HiringManager);
        Guid tenantId = Guid.NewGuid();

        // Act
        string token = _jwtService.GenerateAccessToken(user, tenantId);

        // Assert
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "claims@test.com");
        jwt.Claims.Should().Contain(c => c.Type == "role" && c.Value == UserRole.HiringManager);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ExpiresInConfiguredMinutes()
    {
        // Arrange
        User user = TestData.CreateUser();
        Guid tenantId = Guid.NewGuid();

        // Act
        string token = _jwtService.GenerateAccessToken(user, tenantId);

        // Assert
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwt = handler.ReadJwtToken(token);
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyBase64String()
    {
        // Arrange & Act
        string token = _jwtService.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        byte[] decoded = Convert.FromBase64String(token);
        decoded.Should().HaveCount(64);
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUniqueTokens()
    {
        // Arrange & Act
        string token1 = _jwtService.GenerateRefreshToken();
        string token2 = _jwtService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void HashToken_SameInput_ProducesSameHash()
    {
        // Arrange
        string token = "test-refresh-token";

        // Act
        string hash1 = _jwtService.HashToken(token);
        string hash2 = _jwtService.HashToken(token);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        // Arrange & Act
        string hash1 = _jwtService.HashToken("token-a");
        string hash2 = _jwtService.HashToken("token-b");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void AccessTokenExpirationMinutes_ReturnsConfiguredValue()
    {
        // Arrange & Act & Assert
        _jwtService.AccessTokenExpirationMinutes.Should().Be(30);
    }

    [Fact]
    public void RefreshTokenExpirationDays_ReturnsConfiguredValue()
    {
        // Arrange & Act & Assert
        _jwtService.RefreshTokenExpirationDays.Should().Be(7);
    }
}
