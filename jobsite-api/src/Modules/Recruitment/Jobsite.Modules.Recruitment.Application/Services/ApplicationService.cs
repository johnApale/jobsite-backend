using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Application.Services;

public sealed class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IJobPostingRepository _jobPostingRepository;
    private readonly IResumeOwnershipVerifier _resumeOwnershipVerifier;
    private readonly IUnitOfWork _unitOfWork;

    public ApplicationService(
        IApplicationRepository applicationRepository,
        IJobPostingRepository jobPostingRepository,
        IResumeOwnershipVerifier resumeOwnershipVerifier,
        [FromKeyedServices("recruitment")] IUnitOfWork unitOfWork)
    {
        _applicationRepository = applicationRepository;
        _jobPostingRepository = jobPostingRepository;
        _resumeOwnershipVerifier = resumeOwnershipVerifier;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResponse> SubmitAsync(
        Guid jobPostingId, SubmitApplicationRequest request, Guid applicantId, CancellationToken ct = default)
    {
        JobPosting? jobPosting = await _jobPostingRepository.GetByIdAsync(jobPostingId, ct);

        if (jobPosting is null)
            throw AppErrors.JobPostingNotFound;

        if (jobPosting.Status != JobPostingStatus.Published)
            throw AppErrors.UnprocessableEntity.WithMessage("Job posting is not accepting applications");

        bool alreadyApplied = await _applicationRepository.ExistsByApplicantAndJobAsync(applicantId, jobPostingId, ct);

        if (alreadyApplied)
            throw AppErrors.DuplicateApplication;

        bool resumeOwned = await _resumeOwnershipVerifier.IsOwnedByUserAsync(request.ResumeId, applicantId, ct);

        if (!resumeOwned)
            throw AppErrors.ResumeNotFound;

        List<QuestionAnswerPayload>? questionAnswers = request.QuestionAnswers?.ConvertAll(a =>
            new QuestionAnswerPayload
            {
                QuestionId = a.QuestionId,
                ResponseText = a.ResponseText,
                ResponseData = a.ResponseData
            });

        ApplicationEntity application = new()
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            ApplicantId = applicantId,
            ResumeId = request.ResumeId,
            CoverLetterUrl = request.CoverLetterUrl
        };

        application.Submit(questionAnswers);

        _applicationRepository.Add(application);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(application);
    }

    public async Task<ApplicationResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        ApplicationEntity? application = await _applicationRepository.GetByIdAsync(id, ct);

        if (application is null)
            throw AppErrors.ApplicationNotFound;

        return MapToResponse(application);
    }

    public async Task<ApplicationListResponse> ListAsync(
        ApplicationQueryParameters parameters, CancellationToken ct = default)
    {
        return await _applicationRepository.ListAsync(parameters, ct);
    }

    public async Task<ApplicationResponse> WithdrawAsync(
        Guid id, Guid applicantId, CancellationToken ct = default)
    {
        ApplicationEntity? application = await _applicationRepository.GetByIdForUpdateAsync(id, ct);

        if (application is null)
            throw AppErrors.ApplicationNotFound;

        if (application.ApplicantId != applicantId)
            throw AppErrors.Forbidden;

        if (application.Status == ApplicationStatus.Withdrawn)
            throw AppErrors.ApplicationAlreadyWithdrawn;

        application.Withdraw();
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(application);
    }

    private static ApplicationResponse MapToResponse(ApplicationEntity application)
    {
        return new ApplicationResponse
        {
            Id = application.Id,
            JobPostingId = application.JobPostingId,
            ApplicantId = application.ApplicantId,
            Status = application.Status,
            ResumeId = application.ResumeId,
            CoverLetterUrl = application.CoverLetterUrl,
            RejectionReason = application.RejectionReason,
            RejectedAtStage = application.RejectedAtStage,
            WithdrawnAt = application.WithdrawnAt,
            SubmittedAt = application.SubmittedAt,
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt
        };
    }
}
