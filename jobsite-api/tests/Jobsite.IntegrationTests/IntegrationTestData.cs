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
}
