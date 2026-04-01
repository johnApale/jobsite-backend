namespace Jobsite.Modules.Tenancy.Application.DTOs;

/// <summary>Response shape for tenant data.</summary>
public sealed class TenantResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Subdomain { get; init; }
    public required string Status { get; init; }
    public required string OwnerName { get; init; }
    public required string OwnerEmail { get; init; }
    public required string ContactName { get; init; }
    public required string ContactEmail { get; init; }
    public DateTime? ProvisionedAt { get; init; }
    public DateTime? DeactivatedAt { get; init; }
    public TenantBrandingResponse? Branding { get; init; }
}

/// <summary>Branding subset included in tenant responses.</summary>
public sealed class TenantBrandingResponse
{
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? Tagline { get; init; }
}
