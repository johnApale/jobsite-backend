namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Carries an applicant's answer to a screening question within
/// <see cref="ApplicationSubmittedEvent"/>. The Screening module's event handler
/// persists these into <c>screening.screening_question_responses</c>.
/// </summary>
public sealed class QuestionAnswerPayload
{
    /// <summary>FK to <c>recruitment.job_screening_questions.id</c>.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Free-text answer (for <c>FreeText</c> questions).</summary>
    public string? ResponseText { get; init; }

    /// <summary>
    /// Structured answer data as JSON string.
    /// For <c>MultipleChoice</c>: <c>{"selected_options": [0, 2]}</c>.
    /// For <c>YesNo</c>: <c>{"answer": true}</c>.
    /// </summary>
    public string? ResponseData { get; init; }
}
