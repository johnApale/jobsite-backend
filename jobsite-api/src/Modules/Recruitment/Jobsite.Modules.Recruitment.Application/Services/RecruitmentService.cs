using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Recruitment.Application.Services;

public sealed class RecruitmentService : IRecruitmentService
{
    private readonly IJobPostingRepository _jobPostingRepository;
    private readonly IClientCompanyRepository _clientCompanyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecruitmentService(
        IJobPostingRepository jobPostingRepository,
        IClientCompanyRepository clientCompanyRepository,
        [FromKeyedServices("recruitment")] IUnitOfWork unitOfWork)
    {
        _jobPostingRepository = jobPostingRepository;
        _clientCompanyRepository = clientCompanyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<JobPostingResponse> CreateAsync(
        CreateJobPostingRequest request, Guid postedBy, CancellationToken ct = default)
    {
        if (request.ClientCompanyId is not null)
        {
            bool companyExists = await _clientCompanyRepository.ExistsByIdAsync(request.ClientCompanyId.Value, ct);
            if (!companyExists)
                throw AppErrors.ClientCompanyNotFound;
        }

        JobPosting jobPosting = new()
        {
            Id = Guid.NewGuid(),
            ClientCompanyId = request.ClientCompanyId,
            Title = request.Title,
            Description = request.Description,
            LocationType = request.LocationType,
            City = request.City,
            Country = request.Country,
            EmploymentType = request.EmploymentType,
            SalaryMin = request.SalaryMin,
            SalaryMax = request.SalaryMax,
            SalaryCurrency = request.SalaryCurrency,
            Department = request.Department,
            Status = JobPostingStatus.Draft,
            PostedBy = postedBy,
            ClosesAt = request.ClosesAt
        };

        _jobPostingRepository.Add(jobPosting);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(jobPosting);
    }

    public async Task<JobPostingResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdWithDetailsAsync(id, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        return MapToDetailResponse(jobPosting);
    }

    public async Task<JobPostingListResponse> ListAsync(
        JobPostingQueryParameters parameters, CancellationToken ct = default)
    {
        return await _jobPostingRepository.ListAsync(parameters, ct);
    }

    public async Task<JobPostingResponse> UpdateAsync(
        Guid id, UpdateJobPostingRequest request, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdForUpdateAsync(id, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        if (request.ClientCompanyId is not null)
        {
            bool companyExists = await _clientCompanyRepository.ExistsByIdAsync(request.ClientCompanyId.Value, ct);
            if (!companyExists)
                throw AppErrors.ClientCompanyNotFound;
        }

        if (request.Title is not null)
            jobPosting.Title = request.Title;

        if (request.Description is not null)
            jobPosting.Description = request.Description;

        if (request.LocationType is not null)
            jobPosting.LocationType = request.LocationType;

        if (request.City is not null)
            jobPosting.City = request.City;

        if (request.Country is not null)
            jobPosting.Country = request.Country;

        if (request.EmploymentType is not null)
            jobPosting.EmploymentType = request.EmploymentType;

        if (request.SalaryMin is not null)
            jobPosting.SalaryMin = request.SalaryMin;

        if (request.SalaryMax is not null)
            jobPosting.SalaryMax = request.SalaryMax;

        if (request.SalaryCurrency is not null)
            jobPosting.SalaryCurrency = request.SalaryCurrency;

        if (request.Department is not null)
            jobPosting.Department = request.Department;

        if (request.ClientCompanyId is not null)
            jobPosting.ClientCompanyId = request.ClientCompanyId;

        if (request.ClosesAt is not null)
            jobPosting.ClosesAt = request.ClosesAt;

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(jobPosting);
    }

    public async Task<JobPostingResponse> PublishAsync(Guid id, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdForUpdateAsync(id, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        if (jobPosting.Status != JobPostingStatus.Draft)
            throw AppErrors.UnprocessableEntity.WithMessage("Job posting must be in Draft status to publish");

        jobPosting.Publish();
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(jobPosting);
    }

    public async Task<JobPostingResponse> CloseAsync(Guid id, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdForUpdateAsync(id, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        if (jobPosting.Status != JobPostingStatus.Published)
            throw AppErrors.UnprocessableEntity.WithMessage("Job posting must be in Published status to close");

        jobPosting.Close();
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(jobPosting);
    }

    private static JobPostingResponse MapToResponse(JobPosting jobPosting)
    {
        return new JobPostingResponse
        {
            Id = jobPosting.Id,
            ClientCompanyId = jobPosting.ClientCompanyId,
            Title = jobPosting.Title,
            Description = jobPosting.Description,
            LocationType = jobPosting.LocationType,
            City = jobPosting.City,
            Country = jobPosting.Country,
            EmploymentType = jobPosting.EmploymentType,
            SalaryMin = jobPosting.SalaryMin,
            SalaryMax = jobPosting.SalaryMax,
            SalaryCurrency = jobPosting.SalaryCurrency,
            Department = jobPosting.Department,
            Status = jobPosting.Status,
            PostedBy = jobPosting.PostedBy,
            PublishedAt = jobPosting.PublishedAt,
            ClosesAt = jobPosting.ClosesAt,
            ClosedAt = jobPosting.ClosedAt,
            CreatedAt = jobPosting.CreatedAt,
            UpdatedAt = jobPosting.UpdatedAt
        };
    }

    private static JobPostingResponse MapToDetailResponse(JobPosting jobPosting)
    {
        return new JobPostingResponse
        {
            Id = jobPosting.Id,
            ClientCompanyId = jobPosting.ClientCompanyId,
            Title = jobPosting.Title,
            Description = jobPosting.Description,
            LocationType = jobPosting.LocationType,
            City = jobPosting.City,
            Country = jobPosting.Country,
            EmploymentType = jobPosting.EmploymentType,
            SalaryMin = jobPosting.SalaryMin,
            SalaryMax = jobPosting.SalaryMax,
            SalaryCurrency = jobPosting.SalaryCurrency,
            Department = jobPosting.Department,
            Status = jobPosting.Status,
            PostedBy = jobPosting.PostedBy,
            PublishedAt = jobPosting.PublishedAt,
            ClosesAt = jobPosting.ClosesAt,
            ClosedAt = jobPosting.ClosedAt,
            Criteria = jobPosting.Criteria.ConvertAll(MapCriteriaToResponse),
            Questions = jobPosting.Questions.ConvertAll(MapQuestionToResponse),
            CreatedAt = jobPosting.CreatedAt,
            UpdatedAt = jobPosting.UpdatedAt
        };
    }

    private static CriteriaResponse MapCriteriaToResponse(JobEvaluationCriteria criteria)
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

    private static QuestionResponse MapQuestionToResponse(JobScreeningQuestion question)
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
