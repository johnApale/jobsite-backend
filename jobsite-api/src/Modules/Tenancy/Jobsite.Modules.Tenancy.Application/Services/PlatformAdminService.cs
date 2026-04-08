using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Tenancy.Application.Services;

/// <summary>
/// Platform-wide tenant administration service.
/// Operates against the shared Catalog DB — not scoped to any individual tenant.
/// </summary>
public sealed class PlatformAdminService : IPlatformAdminService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PlatformAdminService(
        ITenantRepository tenantRepository,
        [FromKeyedServices("catalog")] IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantListResponse> GetTenantsAsync(TenantQueryParameters parameters, CancellationToken ct = default)
    {
        (List<Tenant> items, bool hasMore) = await _tenantRepository.GetListAsync(
            parameters.Status,
            parameters.Search,
            parameters.Cursor,
            parameters.PageSize,
            ct);

        List<TenantResponse> responses = items.Select(MapToResponse).ToList();
        string? nextCursor = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;

        return new TenantListResponse
        {
            Items = responses,
            NextCursor = nextCursor,
            HasMore = hasMore
        };
    }

    public async Task<TenantResponse> GetTenantByIdAsync(Guid id, CancellationToken ct = default)
    {
        Tenant? tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            throw AppErrors.TenantNotFound;

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> SuspendTenantAsync(Guid id, CancellationToken ct = default)
    {
        Tenant? tenant = await _tenantRepository.GetByIdForUpdateAsync(id, ct);
        if (tenant is null)
            throw AppErrors.TenantNotFound;

        if (tenant.Status != TenantStatus.Active)
            throw AppErrors.InvalidRequest.WithMessage("Only active tenants can be suspended");

        tenant.Status = TenantStatus.Suspended;
        tenant.DeactivatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> ReactivateTenantAsync(Guid id, CancellationToken ct = default)
    {
        Tenant? tenant = await _tenantRepository.GetByIdForUpdateAsync(id, ct);
        if (tenant is null)
            throw AppErrors.TenantNotFound;

        if (tenant.Status != TenantStatus.Suspended)
            throw AppErrors.InvalidRequest.WithMessage("Only suspended tenants can be reactivated");

        tenant.Status = TenantStatus.Active;
        tenant.DeactivatedAt = null;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(tenant);
    }

    private static TenantResponse MapToResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Subdomain = tenant.Subdomain,
            Status = tenant.Status,
            OwnerName = tenant.OwnerName,
            OwnerEmail = tenant.OwnerEmail,
            ContactName = tenant.ContactName,
            ContactEmail = tenant.ContactEmail,
            ProvisionedAt = tenant.ProvisionedAt,
            DeactivatedAt = tenant.DeactivatedAt,
            Branding = tenant.Branding is null ? null : new TenantBrandingResponse
            {
                LogoUrl = tenant.Branding.LogoUrl,
                FaviconUrl = tenant.Branding.FaviconUrl,
                PrimaryColor = tenant.Branding.PrimaryColor,
                SecondaryColor = tenant.Branding.SecondaryColor,
                Tagline = tenant.Branding.Tagline
            }
        };
    }
}
