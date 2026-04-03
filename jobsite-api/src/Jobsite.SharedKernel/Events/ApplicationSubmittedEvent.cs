using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when an applicant submits an application to a job posting.
/// Consumed by: Screening module (to begin CV screening).
/// </summary>
public sealed class ApplicationSubmittedEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required DateTime SubmittedAt { get; init; }

    /// <summary>
    /// Answers to <c>AtApplication</c> screening questions submitted with the application.
    /// Empty when the job has no <c>AtApplication</c> questions.
    /// The Screening module persists these into <c>screening.screening_question_responses</c>.
    /// </summary>
    public List<QuestionAnswerPayload> QuestionAnswers { get; init; } = [];
}
