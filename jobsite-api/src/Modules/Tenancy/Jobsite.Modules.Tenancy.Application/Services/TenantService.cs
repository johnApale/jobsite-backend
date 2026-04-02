using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;

namespace Jobsite.Modules.Tenancy.Application.Services;

/// <summary>
/// Application service for tenant registration and lookup.
/// </summary>
public sealed class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantProvisioner _tenantProvisioner;
    private readonly IUnitOfWork _unitOfWork;

    public TenantService(
        ITenantRepository tenantRepository,
        ITenantProvisioner tenantProvisioner,
        [FromKeyedServices("catalog")] IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _tenantProvisioner = tenantProvisioner;
        _unitOfWork = unitOfWork;
    }

    public async Task<TenantResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Tenant? tenant = await _tenantRepository.GetByIdAsync(id, ct);
        if (tenant is null)
            throw AppErrors.TenantNotFound;

        return MapToResponse(tenant);
    }

    public async Task<TenantResponse> RegisterAsync(RegisterTenantRequest request, CancellationToken ct = default)
    {
        bool subdomainTaken = await _tenantRepository.SubdomainExistsAsync(request.Subdomain, ct);
        if (subdomainTaken)
            throw AppErrors.InvalidRequest.WithMessage($"Subdomain '{request.Subdomain}' is already taken");

        bool nameTaken = await _tenantRepository.NameExistsAsync(request.Name, ct);
        if (nameTaken)
            throw AppErrors.InvalidRequest.WithMessage($"Company name '{request.Name}' is already taken");

        Tenant tenant = new()
        {
            Name = request.Name,
            Subdomain = request.Subdomain.ToLowerInvariant(),
            ConnectionString = string.Empty, // Set during provisioning
            Status = TenantStatus.Provisioning,
            OwnerName = request.OwnerName,
            OwnerEmail = request.OwnerEmail,
            ContactName = request.OwnerName,
            ContactEmail = request.OwnerEmail
        };

        _tenantRepository.Add(tenant);
        await _unitOfWork.SaveChangesAsync(ct);

        // Provision the tenant database (CREATE DATABASE, update status to Active)
        await _tenantProvisioner.ProvisionAsync(tenant.Id, ct);

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
