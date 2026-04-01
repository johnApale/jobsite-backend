namespace Jobsite.Modules.Tenancy.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/tenants/register</c>.</summary>
public sealed class RegisterTenantRequest
{
    /// <summary>Company display name.</summary>
    public required string Name { get; init; }

    /// <summary>DNS label for <c>{subdomain}.djobsite.com</c>.</summary>
    public required string Subdomain { get; init; }

    /// <summary>Person who registered the tenant.</summary>
    public required string OwnerName { get; init; }

    /// <summary>Seeded as the first AgencyAdmin user.</summary>
    public required string OwnerEmail { get; init; }
}
