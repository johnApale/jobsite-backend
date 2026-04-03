using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Recruitment.Application.Services;

public sealed class ClientCompanyService : IClientCompanyService
{
    private readonly IClientCompanyRepository _clientCompanyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ClientCompanyService(
        IClientCompanyRepository clientCompanyRepository,
        [FromKeyedServices("recruitment")] IUnitOfWork unitOfWork)
    {
        _clientCompanyRepository = clientCompanyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ClientCompanyResponse> CreateAsync(
        CreateClientCompanyRequest request, CancellationToken ct = default)
    {
        ClientCompany clientCompany = new()
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            DisplayName = request.DisplayName,
            IsAnonymous = request.IsAnonymous,
            Industry = request.Industry,
            Website = request.Website,
            ContactName = request.ContactName,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Notes = request.Notes,
            Status = ClientCompanyStatus.Active
        };

        _clientCompanyRepository.Add(clientCompany);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(clientCompany);
    }

    public async Task<ClientCompanyResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ClientCompany? clientCompany = await _clientCompanyRepository.GetByIdAsync(id, ct);

        if (clientCompany is null)
            throw AppErrors.ClientCompanyNotFound;

        return MapToResponse(clientCompany);
    }

    public async Task<ClientCompanyListResponse> ListAsync(
        ClientCompanyQueryParameters parameters, CancellationToken ct = default)
    {
        return await _clientCompanyRepository.ListAsync(parameters, ct);
    }

    public async Task<ClientCompanyResponse> UpdateAsync(
        Guid id, UpdateClientCompanyRequest request, CancellationToken ct = default)
    {
        ClientCompany? clientCompany = await _clientCompanyRepository.GetByIdForUpdateAsync(id, ct);

        if (clientCompany is null)
            throw AppErrors.ClientCompanyNotFound;

        if (request.Name is not null)
            clientCompany.Name = request.Name;

        if (request.DisplayName is not null)
            clientCompany.DisplayName = request.DisplayName;

        if (request.IsAnonymous is not null)
            clientCompany.IsAnonymous = request.IsAnonymous.Value;

        if (request.Industry is not null)
            clientCompany.Industry = request.Industry;

        if (request.Website is not null)
            clientCompany.Website = request.Website;

        if (request.ContactName is not null)
            clientCompany.ContactName = request.ContactName;

        if (request.ContactEmail is not null)
            clientCompany.ContactEmail = request.ContactEmail;

        if (request.ContactPhone is not null)
            clientCompany.ContactPhone = request.ContactPhone;

        if (request.Notes is not null)
            clientCompany.Notes = request.Notes;

        if (request.Status is not null)
            clientCompany.Status = request.Status;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(clientCompany);
    }

    private static ClientCompanyResponse MapToResponse(ClientCompany clientCompany)
    {
        return new ClientCompanyResponse
        {
            Id = clientCompany.Id,
            Name = clientCompany.Name,
            DisplayName = clientCompany.DisplayName,
            IsAnonymous = clientCompany.IsAnonymous,
            Industry = clientCompany.Industry,
            Website = clientCompany.Website,
            ContactName = clientCompany.ContactName,
            ContactEmail = clientCompany.ContactEmail,
            ContactPhone = clientCompany.ContactPhone,
            Notes = clientCompany.Notes,
            Status = clientCompany.Status,
            CreatedAt = clientCompany.CreatedAt,
            UpdatedAt = clientCompany.UpdatedAt
        };
    }
}
