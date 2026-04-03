using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Application service for managing screening questions per job posting.</summary>
public interface IScreeningQuestionService
{
    Task<QuestionResponse> AddAsync(Guid jobPostingId, CreateQuestionRequest request, CancellationToken ct = default);
    Task<List<QuestionResponse>> ListByJobPostingAsync(Guid jobPostingId, CancellationToken ct = default);
    Task<QuestionResponse> UpdateAsync(Guid jobPostingId, Guid questionId, UpdateQuestionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid jobPostingId, Guid questionId, CancellationToken ct = default);
    Task<List<AiQuestionSuggestion>?> SuggestAsync(Guid jobPostingId, CancellationToken ct = default);
}
