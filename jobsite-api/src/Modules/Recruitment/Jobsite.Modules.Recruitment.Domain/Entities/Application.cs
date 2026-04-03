using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Recruitment.Domain.Entities;

/// <summary>
/// An applicant's submission to a specific job posting — maps to <c>recruitment.applications</c>.
/// Central record for the entire hiring pipeline. Every downstream module references
/// the application — not the applicant or job directly.
/// One application per applicant per job, enforced by a unique constraint.
/// </summary>
public sealed class Application : AggregateRoot
{
    /// <summary>FK to <c>recruitment.job_postings</c>.</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>FK to <c>auth.users</c>. Must have role = Applicant.</summary>
    public Guid ApplicantId { get; set; }

    /// <summary>Pipeline status. Constrained by CHECK.</summary>
    public string Status { get; set; } = Constants.ApplicationStatus.Submitted;

    /// <summary>FK to <c>profiles.resumes</c>. The specific resume version at submission time.</summary>
    public Guid ResumeId { get; set; }

    /// <summary>Optional cover letter submitted with this application.</summary>
    public string? CoverLetterUrl { get; set; }

    /// <summary>Set when status moves to Rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Which pipeline stage rejected the candidate. Constrained by CHECK.</summary>
    public string? RejectedAtStage { get; set; }

    /// <summary>Set when applicant withdraws.</summary>
    public DateTime? WithdrawnAt { get; set; }

    /// <summary>When the application was submitted.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Navigation to the job posting.</summary>
    public JobPosting JobPosting { get; set; } = null!;

    /// <summary>
    /// Mark as submitted and raise <see cref="ApplicationSubmittedEvent"/>.
    /// </summary>
    public void Submit(List<QuestionAnswerPayload>? questionAnswers = null)
    {
        SubmittedAt = DateTime.UtcNow;
        Status = Constants.ApplicationStatus.Submitted;

        RaiseDomainEvent(new ApplicationSubmittedEvent
        {
            ApplicationId = Id,
            JobPostingId = JobPostingId,
            ApplicantUserId = ApplicantId,
            SubmittedAt = SubmittedAt,
            QuestionAnswers = questionAnswers ?? []
        });
    }

    /// <summary>Withdraw the application.</summary>
    public void Withdraw()
    {
        Status = Constants.ApplicationStatus.Withdrawn;
        WithdrawnAt = DateTime.UtcNow;
    }
}
