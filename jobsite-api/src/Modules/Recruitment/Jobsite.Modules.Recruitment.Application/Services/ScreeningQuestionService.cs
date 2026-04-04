using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Recruitment.Application.Services;

/// <summary>Minimal settings shape for reading AI assessment feature flag.</summary>
public sealed class AssessmentFeatureFlags
{
    public bool AiAssessmentQuestionsEnabled { get; set; }
}

public sealed class ScreeningQuestionService : IScreeningQuestionService
{
    private readonly IScreeningQuestionRepository _questionRepository;
    private readonly ICriteriaRepository _criteriaRepository;
    private readonly IJobPostingRepository _jobPostingRepository;
    private readonly IAiQuestionSuggester _aiQuestionSuggester;
    private readonly ITenantSettingsReader _tenantSettingsReader;
    private readonly IUnitOfWork _unitOfWork;

    public ScreeningQuestionService(
        IScreeningQuestionRepository questionRepository,
        ICriteriaRepository criteriaRepository,
        IJobPostingRepository jobPostingRepository,
        IAiQuestionSuggester aiQuestionSuggester,
        ITenantSettingsReader tenantSettingsReader,
        [FromKeyedServices("recruitment")] IUnitOfWork unitOfWork)
    {
        _questionRepository = questionRepository;
        _criteriaRepository = criteriaRepository;
        _jobPostingRepository = jobPostingRepository;
        _aiQuestionSuggester = aiQuestionSuggester;
        _tenantSettingsReader = tenantSettingsReader;
        _unitOfWork = unitOfWork;
    }

    public async Task<QuestionResponse> AddAsync(
        Guid jobPostingId, CreateQuestionRequest request, CancellationToken ct = default)
    {
        bool jobExists = await _jobPostingRepository.ExistsByIdAsync(jobPostingId, ct);

        if (!jobExists)
            throw AppErrors.JobPostingNotFound;

        JobScreeningQuestion question = new()
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            QuestionText = request.QuestionText,
            QuestionType = request.QuestionType,
            Timing = request.Timing,
            IsRequired = request.IsRequired,
            Weight = request.Weight,
            ExpectedAnswer = request.ExpectedAnswer,
            Options = request.Options,
            DisplayOrder = request.DisplayOrder
        };

        _questionRepository.Add(question);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(question);
    }

    public async Task<List<QuestionResponse>> ListByJobPostingAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        bool jobExists = await _jobPostingRepository.ExistsByIdAsync(jobPostingId, ct);

        if (!jobExists)
            throw AppErrors.JobPostingNotFound;

        List<JobScreeningQuestion> questions = await _questionRepository.GetByJobPostingIdAsync(jobPostingId, ct);
        return questions.ConvertAll(MapToResponse);
    }

    public async Task<QuestionResponse> UpdateAsync(
        Guid jobPostingId, Guid questionId, UpdateQuestionRequest request, CancellationToken ct = default)
    {
        JobScreeningQuestion? question = await _questionRepository.GetByIdForUpdateAsync(questionId, ct);

        if (question is null || question.JobPostingId != jobPostingId)
            throw AppErrors.ScreeningQuestionNotFound;

        if (request.QuestionText is not null)
            question.QuestionText = request.QuestionText;

        if (request.QuestionType is not null)
            question.QuestionType = request.QuestionType;

        if (request.Timing is not null)
            question.Timing = request.Timing;

        if (request.IsRequired is not null)
            question.IsRequired = request.IsRequired.Value;

        if (request.Weight is not null)
            question.Weight = request.Weight.Value;

        if (request.ExpectedAnswer is not null)
            question.ExpectedAnswer = request.ExpectedAnswer;

        if (request.Options is not null)
            question.Options = request.Options;

        if (request.DisplayOrder is not null)
            question.DisplayOrder = request.DisplayOrder.Value;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(question);
    }

    public async Task DeleteAsync(
        Guid jobPostingId, Guid questionId, CancellationToken ct = default)
    {
        JobScreeningQuestion? question = await _questionRepository.GetByIdForUpdateAsync(questionId, ct);

        if (question is null || question.JobPostingId != jobPostingId)
            throw AppErrors.ScreeningQuestionNotFound;

        _questionRepository.Remove(question);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<AiQuestionSuggestion>?> SuggestAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        AssessmentFeatureFlags? settings =
            await _tenantSettingsReader.GetSettingAsync<AssessmentFeatureFlags>("assessment_settings", ct);

        if (settings is null || !settings.AiAssessmentQuestionsEnabled)
            return null;

        JobPosting? jobPosting = await _jobPostingRepository.GetByIdAsync(jobPostingId, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        List<JobEvaluationCriteria> criteriaEntities =
            await _criteriaRepository.GetByJobPostingIdAsync(jobPostingId, ct);

        List<CriteriaResponse> criteria = criteriaEntities.ConvertAll(c => new CriteriaResponse
        {
            Id = c.Id,
            JobPostingId = c.JobPostingId,
            Name = c.Name,
            Category = c.Category,
            EvaluationMethod = c.EvaluationMethod,
            IsRequired = c.IsRequired,
            Weight = c.Weight,
            Configuration = c.Configuration,
            DisplayOrder = c.DisplayOrder,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        });

        return await _aiQuestionSuggester.SuggestAsync(jobPosting.Description, criteria, ct);
    }

    private static QuestionResponse MapToResponse(JobScreeningQuestion question)
    {
        return new QuestionResponse
        {
            Id = question.Id,
            JobPostingId = question.JobPostingId,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            Timing = question.Timing,
            IsRequired = question.IsRequired,
            Weight = question.Weight,
            ExpectedAnswer = question.ExpectedAnswer,
            Options = question.Options,
            DisplayOrder = question.DisplayOrder,
            CreatedAt = question.CreatedAt,
            UpdatedAt = question.UpdatedAt
        };
    }
}
