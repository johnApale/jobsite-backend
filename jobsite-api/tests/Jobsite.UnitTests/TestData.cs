using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.UnitTests;

/// <summary>
/// Factory methods for test data. Avoids inline object construction in tests.
/// </summary>
public static class TestData
{
    public static Tenant CreateTenant(
        string? name = null,
        string? subdomain = null,
        string? status = null,
        string? ownerEmail = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name ?? "Acme Corp",
        Subdomain = subdomain ?? "acme",
        ConnectionString = "Host=localhost;Database=tenant_acme",
        Status = status ?? TenantStatus.Active,
        OwnerName = "John Doe",
        OwnerEmail = ownerEmail ?? "john@acme.com",
        ContactName = "John Doe",
        ContactEmail = ownerEmail ?? "john@acme.com",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static TenantBranding CreateTenantBranding(Guid? tenantId = null) => new()
    {
        TenantId = tenantId ?? Guid.NewGuid(),
        LogoUrl = "https://cdn.example.com/logo.png",
        FaviconUrl = "https://cdn.example.com/favicon.ico",
        PrimaryColor = "#1A73E8",
        SecondaryColor = "#FFFFFF",
        Tagline = "Test tagline",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static RegisterTenantRequest CreateRegisterTenantRequest(
        string? name = null,
        string? subdomain = null) => new()
    {
        Name = name ?? "Acme Corp",
        Subdomain = subdomain ?? "acme",
        OwnerName = "John Doe",
        OwnerEmail = "john@acme.com"
    };

    // ── Auth Module ──────────────────────────────────────────────────────

    public static User CreateUser(
        string? email = null,
        string? passwordHash = null,
        string? role = null,
        string? status = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = email ?? "test@example.com",
        PasswordHash = passwordHash ?? "$2a$12$fakehashfakehashfakehashfakehashfakehashfakehashfakeh",
        EmailVerified = false,
        Role = role ?? UserRole.Applicant,
        Status = status ?? UserStatus.Active,
        FirstName = "Test",
        LastName = "User",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static RefreshToken CreateRefreshToken(
        Guid? userId = null,
        string? tokenHash = null,
        Guid? familyId = null,
        bool isRevoked = false,
        DateTime? expiresAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        TokenHash = tokenHash ?? "test-token-hash",
        FamilyId = familyId ?? Guid.NewGuid(),
        IsRevoked = isRevoked,
        ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static RegisterRequest CreateRegisterRequest(
        string? email = null,
        string? password = null,
        string? role = null) => new()
    {
        Email = email ?? "new@example.com",
        Password = password ?? "Password123!",
        FirstName = "New",
        LastName = "User",
        Role = role
    };

    public static LoginRequest CreateLoginRequest(
        string? email = null,
        string? password = null) => new()
    {
        Email = email ?? "test@example.com",
        Password = password ?? "Password123!"
    };
}
