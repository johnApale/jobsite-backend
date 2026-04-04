using Jobsite.Modules.Screening.Domain.Entities;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>Repository for <c>screening.screening_question_responses</c>.</summary>
public interface IScreeningQuestionResponseRepository
{
    Task<List<ScreeningQuestionResponse>> GetByApplicationIdAsync(
        Guid applicationId, CancellationToken ct = default);

    Task<List<ScreeningQuestionResponse>> GetByApplicationIdAndTimingAsync(
        Guid applicationId, string timing, CancellationToken ct = default);

    Task<bool> ExistsByApplicationAndQuestionAsync(
        Guid applicationId, Guid questionId, CancellationToken ct = default);

    void Add(ScreeningQuestionResponse response);

    void AddRange(IEnumerable<ScreeningQuestionResponse> responses);
}
