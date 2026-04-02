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
}
