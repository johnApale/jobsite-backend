using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Recruitment.Application.Services;

public sealed class CriteriaService : ICriteriaService
{
    private readonly ICriteriaRepository _criteriaRepository;
    private readonly IJobPostingRepository _jobPostingRepository;
    private readonly IAiCriteriaSuggester _aiCriteriaSuggester;
    private readonly IUnitOfWork _unitOfWork;

    public CriteriaService(
        ICriteriaRepository criteriaRepository,
        IJobPostingRepository jobPostingRepository,
        IAiCriteriaSuggester aiCriteriaSuggester,
        [FromKeyedServices("recruitment")] IUnitOfWork unitOfWork)
    {
        _criteriaRepository = criteriaRepository;
        _jobPostingRepository = jobPostingRepository;
        _aiCriteriaSuggester = aiCriteriaSuggester;
        _unitOfWork = unitOfWork;
    }

    public async Task<CriteriaResponse> AddAsync(
        Guid jobPostingId, CreateCriteriaRequest request, CancellationToken ct = default)
    {
        bool jobExists = await _jobPostingRepository.ExistsByIdAsync(jobPostingId, ct);

        if (!jobExists)
            throw AppErrors.JobPostingNotFound;

        JobEvaluationCriteria criteria = new()
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            Name = request.Name,
            Category = request.Category,
            EvaluationMethod = request.EvaluationMethod,
            IsRequired = request.IsRequired,
            Weight = request.Weight,
            Configuration = request.Configuration,
            DisplayOrder = request.DisplayOrder
        };

        _criteriaRepository.Add(criteria);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(criteria);
    }

    public async Task<List<CriteriaResponse>> ListByJobPostingAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        bool jobExists = await _jobPostingRepository.ExistsByIdAsync(jobPostingId, ct);

        if (!jobExists)
            throw AppErrors.JobPostingNotFound;

        List<JobEvaluationCriteria> criteria = await _criteriaRepository.GetByJobPostingIdAsync(jobPostingId, ct);
        return criteria.ConvertAll(MapToResponse);
    }

    public async Task<CriteriaResponse> UpdateAsync(
        Guid jobPostingId, Guid criteriaId, UpdateCriteriaRequest request, CancellationToken ct = default)
    {
        JobEvaluationCriteria? criteria = await _criteriaRepository.GetByIdForUpdateAsync(criteriaId, ct);

        if (criteria is null || criteria.JobPostingId != jobPostingId)
            throw AppErrors.CriteriaNotFound;

        if (request.Name is not null)
            criteria.Name = request.Name;

        if (request.Category is not null)
            criteria.Category = request.Category;

        if (request.EvaluationMethod is not null)
            criteria.EvaluationMethod = request.EvaluationMethod;

        if (request.IsRequired is not null)
            criteria.IsRequired = request.IsRequired.Value;

        if (request.Weight is not null)
            criteria.Weight = request.Weight.Value;

        if (request.Configuration is not null)
            criteria.Configuration = request.Configuration;

        if (request.DisplayOrder is not null)
            criteria.DisplayOrder = request.DisplayOrder.Value;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(criteria);
    }

    public async Task DeleteAsync(
        Guid jobPostingId, Guid criteriaId, CancellationToken ct = default)
    {
        JobEvaluationCriteria? criteria = await _criteriaRepository.GetByIdForUpdateAsync(criteriaId, ct);

        if (criteria is null || criteria.JobPostingId != jobPostingId)
            throw AppErrors.CriteriaNotFound;

        _criteriaRepository.Remove(criteria);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<AiCriteriaSuggestion>?> SuggestAsync(
        Guid jobPostingId, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdAsync(jobPostingId, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        return await _aiCriteriaSuggester.SuggestAsync(jobPosting.Title, jobPosting.Description, ct);
    }

    private static CriteriaResponse MapToResponse(JobEvaluationCriteria criteria)
    {
        return new CriteriaResponse
        {
            Id = criteria.Id,
            JobPostingId = criteria.JobPostingId,
            Name = criteria.Name,
            Category = criteria.Category,
            EvaluationMethod = criteria.EvaluationMethod,
            IsRequired = criteria.IsRequired,
            Weight = criteria.Weight,
            Configuration = criteria.Configuration,
            DisplayOrder = criteria.DisplayOrder,
            CreatedAt = criteria.CreatedAt,
            UpdatedAt = criteria.UpdatedAt
        };
    }
}
