using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.EventHandlers;

public sealed class ApplicationSubmittedScreeningHandler : INotificationHandler<ApplicationSubmittedEvent>
{
    private readonly IScreeningResultRepository _resultRepository;
    private readonly IScreeningQuestionResponseRepository _responseRepository;
    private readonly IScreeningService _screeningService;
    private readonly IJobScreeningQuestionsReader _questionsReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<ApplicationSubmittedScreeningHandler> _logger;

    public ApplicationSubmittedScreeningHandler(
        IScreeningResultRepository resultRepository,
        IScreeningQuestionResponseRepository responseRepository,
        IScreeningService screeningService,
        IJobScreeningQuestionsReader questionsReader,
        [FromKeyedServices("screening")] IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<ApplicationSubmittedScreeningHandler> logger)
    {
        _resultRepository = resultRepository;
        _responseRepository = responseRepository;
        _screeningService = screeningService;
        _questionsReader = questionsReader;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(ApplicationSubmittedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling ApplicationSubmittedEvent for application {ApplicationId}",
            notification.ApplicationId);

        // 1. Create the ScreeningResult (shared PK with application)
        ScreeningResult result = new()
        {
            ApplicationId = notification.ApplicationId,
            Status = ScreeningStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _resultRepository.Add(result);

        // 2. Store AtApplication question answers
        if (notification.QuestionAnswers.Count > 0)
        {
            DateTime now = DateTime.UtcNow;
            foreach (QuestionAnswerPayload answer in notification.QuestionAnswers)
            {
                ScreeningQuestionResponse response = new()
                {
                    ApplicationId = notification.ApplicationId,
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
            notification.ApplicationId,
            notification.JobPostingId,
            notification.ApplicantUserId,
            resumeId: null, // Resume ID is resolved inside the pipeline via IApplicantDataReader
            ct);

        // 4. Reload result to get final status
        ScreeningResult? completed = await _resultRepository.GetByApplicationIdAsync(notification.ApplicationId, ct);

        if (completed is not null && completed.Status == ScreeningStatus.Completed)
        {
            bool passed = completed.Outcome is ScreeningOutcome.AutoAdvanced
                or ScreeningOutcome.ManualReview;

            await _publisher.Publish(new CvScreeningCompletedEvent
            {
                ApplicationId = notification.ApplicationId,
                ScreeningResultId = completed.ApplicationId, // shared PK
                PassedScreening = passed,
                CompletedAt = completed.CompletedAt ?? DateTime.UtcNow
            }, ct);
        }
    }
}
