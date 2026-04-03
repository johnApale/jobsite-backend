using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Application service for managing evaluation criteria per job posting.</summary>
public interface ICriteriaService
{
    Task<CriteriaResponse> AddAsync(Guid jobPostingId, CreateCriteriaRequest request, CancellationToken ct = default);
    Task<List<CriteriaResponse>> ListByJobPostingAsync(Guid jobPostingId, CancellationToken ct = default);
    Task<CriteriaResponse> UpdateAsync(Guid jobPostingId, Guid criteriaId, UpdateCriteriaRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid jobPostingId, Guid criteriaId, CancellationToken ct = default);
    Task<List<AiCriteriaSuggestion>?> SuggestAsync(Guid jobPostingId, CancellationToken ct = default);
}
