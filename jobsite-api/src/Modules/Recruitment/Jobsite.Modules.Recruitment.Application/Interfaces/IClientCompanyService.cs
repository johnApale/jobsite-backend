using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Application service for client company CRUD.</summary>
public interface IClientCompanyService
{
    Task<ClientCompanyResponse> CreateAsync(CreateClientCompanyRequest request, CancellationToken ct = default);
    Task<ClientCompanyResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ClientCompanyListResponse> ListAsync(ClientCompanyQueryParameters parameters, CancellationToken ct = default);
    Task<ClientCompanyResponse> UpdateAsync(Guid id, UpdateClientCompanyRequest request, CancellationToken ct = default);
}
