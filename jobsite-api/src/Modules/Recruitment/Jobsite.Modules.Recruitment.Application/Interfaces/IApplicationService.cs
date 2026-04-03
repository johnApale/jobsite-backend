using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Application service for application submission, listing, and withdrawal.</summary>
public interface IApplicationService
{
    Task<ApplicationResponse> SubmitAsync(Guid jobPostingId, SubmitApplicationRequest request, Guid applicantId, CancellationToken ct = default);
    Task<ApplicationResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApplicationListResponse> ListAsync(ApplicationQueryParameters parameters, CancellationToken ct = default);
    Task<ApplicationResponse> WithdrawAsync(Guid id, Guid applicantId, CancellationToken ct = default);
}
