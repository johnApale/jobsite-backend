using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.EventHandlers;

public sealed class ApplicationSubmittedScreeningHandler : IDomainEventHandler<ApplicationSubmittedEvent>
{
    private readonly IScreeningResultRepository _resultRepository;
    private readonly IScreeningQuestionResponseRepository _responseRepository;
    private readonly IScreeningService _screeningService;
    private readonly IJobScreeningQuestionsReader _questionsReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ILogger<ApplicationSubmittedScreeningHandler> _logger;

    public ApplicationSubmittedScreeningHandler(
        IScreeningResultRepository resultRepository,
        IScreeningQuestionResponseRepository responseRepository,
        IScreeningService screeningService,
        IJobScreeningQuestionsReader questionsReader,
        [FromKeyedServices("screening")] IUnitOfWork unitOfWork,
        IDomainEventDispatcher dispatcher,
        ILogger<ApplicationSubmittedScreeningHandler> logger)
    {
        _resultRepository = resultRepository;
        _responseRepository = responseRepository;
        _screeningService = screeningService;
        _questionsReader = questionsReader;
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task HandleAsync(ApplicationSubmittedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling ApplicationSubmittedEvent for application {ApplicationId}",
            domainEvent.ApplicationId);

        // 1. Create the ScreeningResult (shared PK with application)
        ScreeningResult result = new()
        {
            ApplicationId = domainEvent.ApplicationId,
            Status = ScreeningStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _resultRepository.Add(result);

        // 2. Store AtApplication question answers
        if (domainEvent.QuestionAnswers.Count > 0)
        {
            DateTime now = DateTime.UtcNow;
            foreach (QuestionAnswerPayload answer in domainEvent.QuestionAnswers)
            {
                ScreeningQuestionResponse response = new()
                {
                    ApplicationId = domainEvent.ApplicationId,
                    QuestionId = answer.QuestionId,
                    ResponseText = answer.ResponseText,
                    ResponseData = answer.ResponseData,
                    SubmittedAt = now
                };
                _responseRepository.Add(response);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        // 3. Run the screening pipeline
        await _screeningService.ProcessScreeningAsync(
            domainEvent.ApplicationId,
            domainEvent.JobPostingId,
            domainEvent.ApplicantUserId,
            resumeId: null, // Resume ID is resolved inside the pipeline via IApplicantDataReader
            ct);

        // 4. Reload result to get final status
        ScreeningResult? completed = await _resultRepository.GetByApplicationIdAsync(domainEvent.ApplicationId, ct);

        if (completed is not null && completed.Status == ScreeningStatus.Completed)
        {
            bool passed = completed.Outcome is ScreeningOutcome.AutoAdvanced
                or ScreeningOutcome.ManualReview;

            await _dispatcher.DispatchAsync(new CvScreeningCompletedEvent
            {
                ApplicationId = domainEvent.ApplicationId,
                ScreeningResultId = completed.ApplicationId, // shared PK
                PassedScreening = passed,
                CompletedAt = completed.CompletedAt ?? DateTime.UtcNow
            }, ct);
        }
    }
}
