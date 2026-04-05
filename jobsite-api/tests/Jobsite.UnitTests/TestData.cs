using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Tenancy.Application.DTOs;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;
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

    // ── Recruitment Module ──────────────────────────────────────────────

    public static JobPosting CreateJobPosting(
        Guid? id = null,
        string? title = null,
        string? description = null,
        string? requirements = null,
        string? locationType = null,
        string? employmentType = null,
        string? status = null,
        Guid? postedBy = null,
        Guid? clientCompanyId = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        string? salaryCurrency = null,
        string? city = null,
        string? country = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        ClientCompanyId = clientCompanyId,
        Title = title ?? "Senior .NET Developer",
        Description = description ?? "We are looking for an experienced .NET developer.",
        Requirements = requirements,
        LocationType = locationType ?? LocationType.Remote,
        City = city,
        Country = country,
        EmploymentType = employmentType ?? EmploymentType.FullTime,
        SalaryMin = salaryMin,
        SalaryMax = salaryMax,
        SalaryCurrency = salaryCurrency,
        Status = status ?? JobPostingStatus.Draft,
        PostedBy = postedBy ?? Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static ClientCompany CreateClientCompany(
        Guid? id = null,
        string? name = null,
        string? status = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name ?? "Acme Technologies",
        DisplayName = null,
        IsAnonymous = false,
        Industry = Industry.Technology,
        Status = status ?? ClientCompanyStatus.Active,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static ApplicationEntity CreateApplication(
        Guid? id = null,
        Guid? jobPostingId = null,
        Guid? applicantId = null,
        Guid? resumeId = null,
        string? status = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        ApplicantId = applicantId ?? Guid.NewGuid(),
        ResumeId = resumeId ?? Guid.NewGuid(),
        Status = status ?? ApplicationStatus.Submitted,
        SubmittedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static JobEvaluationCriteria CreateCriteria(
        Guid? id = null,
        Guid? jobPostingId = null,
        string? name = null,
        string? category = null,
        string? evaluationMethod = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        Name = name ?? "C# Proficiency",
        Category = category ?? CriteriaCategory.Skill,
        EvaluationMethod = evaluationMethod ?? EvaluationMethod.SemanticSimilarity,
        IsRequired = true,
        Weight = 25.0m,
        Configuration = """{"skill_name":"C#","min_level":"Advanced"}""",
        DisplayOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static JobScreeningQuestion CreateScreeningQuestion(
        Guid? id = null,
        Guid? jobPostingId = null,
        string? questionText = null,
        string? questionType = null,
        string? timing = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        QuestionText = questionText ?? "Do you have 5+ years of .NET experience?",
        QuestionType = questionType ?? QuestionType.YesNo,
        Timing = timing ?? QuestionTiming.AtApplication,
        IsRequired = true,
        Weight = 10.0m,
        DisplayOrder = 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static CreateJobPostingRequest CreateJobPostingRequest(
        string? title = null,
        string? locationType = null,
        string? employmentType = null,
        string? city = null,
        string? country = null,
        decimal? salaryMin = null,
        decimal? salaryMax = null,
        string? salaryCurrency = null) => new()
    {
        Title = title ?? "Senior .NET Developer",
        Description = "We are looking for an experienced .NET developer.",
        LocationType = locationType ?? LocationType.Remote,
        City = city,
        Country = country,
        EmploymentType = employmentType ?? EmploymentType.FullTime,
        SalaryMin = salaryMin,
        SalaryMax = salaryMax,
        SalaryCurrency = salaryCurrency
    };

    public static CreateClientCompanyRequest CreateClientCompanyRequest(
        string? name = null) => new()
    {
        Name = name ?? "Acme Technologies"
    };

    public static SubmitApplicationRequest CreateSubmitApplicationRequest(
        Guid? resumeId = null) => new()
    {
        ResumeId = resumeId ?? Guid.NewGuid()
    };

    public static CreateCriteriaRequest CreateCriteriaRequest(
        string? name = null,
        string? category = null,
        string? evaluationMethod = null) => new()
    {
        Name = name ?? "C# Proficiency",
        Category = category ?? CriteriaCategory.Skill,
        EvaluationMethod = evaluationMethod ?? EvaluationMethod.SemanticSimilarity,
        IsRequired = true,
        Weight = 25.0m,
        Configuration = """{"skill_name":"C#","min_level":"Advanced"}"""
    };

    public static CreateQuestionRequest CreateQuestionRequest(
        string? questionText = null,
        string? questionType = null,
        string? timing = null) => new()
    {
        QuestionText = questionText ?? "Do you have 5+ years of .NET experience?",
        QuestionType = questionType ?? QuestionType.YesNo,
        Timing = timing ?? QuestionTiming.AtApplication,
        IsRequired = true,
        Weight = 10.0m
    };

    // ── Matching ──────────────────────────────────────────────────────────

    public static CandidateMatch CreateCandidateMatch(
        Guid? applicationId = null,
        Guid? jobPostingId = null,
        Guid? applicantUserId = null,
        decimal? screeningScore = null,
        decimal? assessmentScore = null,
        decimal? compositeScore = null,
        string? matchStrength = null) => new()
    {
        ApplicationId = applicationId ?? Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        ApplicantUserId = applicantUserId ?? Guid.NewGuid(),
        ScreeningScore = screeningScore ?? 75m,
        AssessmentScore = assessmentScore,
        CompositeScore = compositeScore ?? screeningScore ?? 75m,
        MatchStrength = matchStrength ?? MatchStrength.Good,
        ScreeningCompletedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static Shortlist CreateShortlist(
        Guid? jobPostingId = null,
        string? status = null,
        string? generatedBy = null,
        int? totalCandidates = null) => new()
    {
        Id = Guid.NewGuid(),
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        Status = status ?? ShortlistStatus.Draft,
        GeneratedBy = generatedBy ?? ShortlistCandidateSource.Algorithm,
        TotalCandidates = totalCandidates ?? 0,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    public static ShortlistCandidate CreateShortlistCandidate(
        Guid? shortlistId = null,
        Guid? applicationId = null,
        Guid? applicantUserId = null,
        decimal? compositeScore = null,
        int? rank = null,
        string? source = null) => new()
    {
        Id = Guid.NewGuid(),
        ShortlistId = shortlistId ?? Guid.NewGuid(),
        ApplicationId = applicationId ?? Guid.NewGuid(),
        ApplicantUserId = applicantUserId ?? Guid.NewGuid(),
        CompositeScore = compositeScore ?? 80m,
        Rank = rank ?? 1,
        Source = source ?? ShortlistCandidateSource.Algorithm,
        AddedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
