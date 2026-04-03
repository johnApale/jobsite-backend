using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Entities;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Repository for client company lookups and persistence.</summary>
public interface IClientCompanyRepository
{
    /// <summary>Get a client company by ID (read-only).</summary>
    Task<ClientCompany?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a client company by ID with tracking enabled (for updates).</summary>
    Task<ClientCompany?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Query client companies with cursor-based pagination and optional filters.</summary>
    Task<ClientCompanyListResponse> ListAsync(ClientCompanyQueryParameters parameters, CancellationToken ct = default);

    /// <summary>Check if a client company exists.</summary>
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persist a new client company.</summary>
    void Add(ClientCompany clientCompany);
}
