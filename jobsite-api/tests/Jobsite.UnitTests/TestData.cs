using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Domain.Entities;
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

    // ── Admin Module ─────────────────────────────────────────────────────

    public static CompanySettings CreateCompanySettings(
        string? defaultTimezone = null,
        string? defaultCurrency = null) => new()
    {
        Id = Guid.NewGuid(),
        DefaultTimezone = defaultTimezone ?? "UTC",
        DefaultCurrency = defaultCurrency ?? "USD",
        AuthSettings = System.Text.Json.JsonSerializer.Serialize(new AuthSettingsDto(), AdminJsonOptions),
        ProfileSettings = System.Text.Json.JsonSerializer.Serialize(new ProfileSettingsDto(), AdminJsonOptions),
        ScreeningSettings = System.Text.Json.JsonSerializer.Serialize(new ScreeningSettingsDto(), AdminJsonOptions),
        MatchingSettings = System.Text.Json.JsonSerializer.Serialize(new MatchingSettingsDto(), AdminJsonOptions),
        AssessmentSettings = System.Text.Json.JsonSerializer.Serialize(new AssessmentSettingsDto(), AdminJsonOptions),
        NotificationSettings = System.Text.Json.JsonSerializer.Serialize(new NotificationSettingsDto(), AdminJsonOptions),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static AuditLog CreateAuditLog(
        string? action = null,
        string? entityType = null,
        Guid? actorId = null,
        Guid? entityId = null) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = actorId ?? Guid.NewGuid(),
        ActorEmail = "admin@example.com",
        ActorRole = "AgencyAdmin",
        Action = action ?? AuditAction.SettingsUpdated,
        EntityType = entityType ?? AuditEntityType.CompanySettings,
        EntityId = entityId,
        Details = null,
        IpAddress = "127.0.0.1",
        UserAgent = "TestAgent/1.0",
        PerformedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static UpdateCompanySettingsRequest CreateUpdateSettingsRequest(
        string? defaultTimezone = null,
        string? defaultCurrency = null) => new()
    {
        DefaultTimezone = defaultTimezone,
        DefaultCurrency = defaultCurrency
    };

    // ── Profiles Module ─────────────────────────────────────────────────

    public static ApplicantProfile CreateApplicantProfile(
        Guid? id = null,
        string? firstName = null,
        string? lastName = null,
        string? phone = null,
        string? city = null,
        string? country = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        FirstName = firstName ?? "Test",
        LastName = lastName ?? "Applicant",
        Phone = phone,
        City = city ?? "Manila",
        Country = country ?? "Philippines",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static Resume CreateResume(
        Guid? id = null,
        Guid? userId = null,
        string? fileName = null,
        string? fileType = null,
        long? fileSizeBytes = null,
        bool isLatest = true,
        bool isParsed = false) => new()
    {
        Id = id ?? Guid.NewGuid(),
        UserId = userId ?? Guid.NewGuid(),
        FileUrl = "/uploads/resumes/test-resume.pdf",
        OriginalFilename = fileName ?? "test-resume.pdf",
        FileSizeBytes = fileSizeBytes ?? 1024,
        FileType = fileType ?? "PDF",
        IsLatest = isLatest,
        IsParsed = isParsed,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static CreateProfileRequest CreateProfileRequest(
        string? firstName = null,
        string? lastName = null) => new()
    {
        FirstName = firstName ?? "Test",
        LastName = lastName ?? "Applicant",
        Phone = "+639171234567",
        City = "Manila",
        Country = "Philippines"
    };

    public static UpdateProfileRequest CreateUpdateProfileRequest(
        string? firstName = null,
        string? lastName = null) => new()
    {
        FirstName = firstName,
        LastName = lastName
    };

    private static readonly System.Text.Json.JsonSerializerOptions AdminJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };
}
