using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.IntegrationTests;

/// <summary>
/// Factory methods for integration test data.
/// </summary>
public static class IntegrationTestData
{
    public static Tenant CreateTenant(
        string? name = null,
        string? subdomain = null,
        string? status = null) => new()
    {
        Name = name ?? $"Tenant-{Guid.NewGuid():N[..8]}",
        Subdomain = subdomain ?? $"t-{Guid.NewGuid():N}"[..12],
        ConnectionString = "Host=localhost;Database=test_tenant",
        Status = status ?? TenantStatus.Active,
        OwnerName = "Test Owner",
        OwnerEmail = "owner@test.com",
        ContactName = "Test Contact",
        ContactEmail = "contact@test.com"
    };

    public static TenantBranding CreateBranding(Guid tenantId) => new()
    {
        TenantId = tenantId,
        LogoUrl = "https://cdn.example.com/logo.png",
        PrimaryColor = "#1A73E8",
        SecondaryColor = "#FFFFFF",
        Tagline = "Integration test branding"
    };

    public static User CreateUser(
        string? email = null,
        string? passwordHash = null,
        string? role = null,
        string? status = null,
        string? firstName = null,
        string? lastName = null) => new()
    {
        Email = email ?? $"user-{Guid.NewGuid():N}"[..20] + "@test.com",
        PasswordHash = passwordHash ?? "$2a$12$LJ3m4ys3LzxJOxBi0TjXi.n3MvCy3GvGfN3vKylMNHXb5G7dMibKq",
        EmailVerified = false,
        Role = role ?? UserRole.Applicant,
        Status = status ?? UserStatus.Active,
        FirstName = firstName ?? "Test",
        LastName = lastName ?? "User"
    };

    public static RefreshToken CreateRefreshToken(
        Guid userId,
        string? tokenHash = null,
        Guid? familyId = null,
        DateTime? expiresAt = null) => new()
    {
        UserId = userId,
        TokenHash = tokenHash ?? Guid.NewGuid().ToString("N"),
        FamilyId = familyId ?? Guid.NewGuid(),
        ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30)
    };

    public static UserExternalLogin CreateExternalLogin(
        Guid userId,
        string? provider = null,
        string? subjectId = null) => new()
    {
        UserId = userId,
        Provider = provider ?? ExternalLoginProvider.Google,
        ProviderSubjectId = subjectId ?? Guid.NewGuid().ToString(),
        ProviderEmail = "oauth@test.com",
        LinkedAt = DateTime.UtcNow
    };

    public static ApplicantProfile CreateApplicantProfile(
        Guid? userId = null,
        string? firstName = null,
        string? lastName = null,
        string? city = null,
        string? country = null) => new()
    {
        Id = userId ?? Guid.NewGuid(),
        FirstName = firstName ?? "Test",
        LastName = lastName ?? "Applicant",
        City = city,
        Country = country
    };

    public static Resume CreateResume(
        Guid userId,
        string? fileType = null,
        bool isLatest = false) => new()
    {
        UserId = userId,
        FileUrl = $"https://blob.storage/resumes/{Guid.NewGuid():N}.pdf",
        OriginalFilename = $"resume-{Guid.NewGuid():N[..8]}.pdf",
        FileSizeBytes = 1024,
        FileType = fileType ?? FileType.Pdf,
        IsLatest = isLatest
    };

    public static CandidateMatch CreateCandidateMatch(
        Guid? applicationId = null,
        Guid? jobPostingId = null,
        decimal compositeScore = 75m,
        string? matchStrength = null) => new()
    {
        ApplicationId = applicationId ?? Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        ApplicantUserId = Guid.NewGuid(),
        ScreeningScore = compositeScore,
        CompositeScore = compositeScore,
        MatchStrength = matchStrength ?? MatchStrength.FromScore(compositeScore),
        ScreeningCompletedAt = DateTime.UtcNow
    };

    public static Shortlist CreateShortlist(
        Guid? jobPostingId = null,
        string? status = null) => new()
    {
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        Status = status ?? ShortlistStatus.Draft,
        GeneratedBy = "Algorithm",
        TotalCandidates = 0
    };

    public static ShortlistCandidate CreateShortlistCandidate(
        Guid shortlistId,
        Guid? applicationId = null,
        int rank = 1,
        string? source = null) => new()
    {
        ShortlistId = shortlistId,
        ApplicationId = applicationId ?? Guid.NewGuid(),
        ApplicantUserId = Guid.NewGuid(),
        CompositeScore = 80m,
        Rank = rank,
        Source = source ?? ShortlistCandidateSource.Algorithm,
        AddedAt = DateTime.UtcNow
    };
}
