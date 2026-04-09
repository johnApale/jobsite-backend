using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.HRWorkflows.Domain.Entities;

/// <summary>
/// A formal job offer extended to a candidate — maps to <c>hr_workflows.job_offers</c>.
/// Uses shared primary key with <c>recruitment.applications</c> (ApplicationId is both PK and FK).
/// One offer per application.
/// </summary>
public sealed class JobOffer : Entity
{
    /// <summary>Shared PK with <c>recruitment.applications.id</c>.</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Ref to <c>recruitment.client_companies.id</c> (cross-module). Null if tenant is hiring for themselves.</summary>
    public Guid? ClientCompanyId { get; set; }

    /// <summary>Lifecycle status: Draft, Pending, Accepted, Declined, Withdrawn, Expired.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Offered salary amount.</summary>
    public decimal Salary { get; set; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR).</summary>
    public string SalaryCurrency { get; set; } = null!;

    /// <summary>Pay period: Annual, Monthly, Hourly.</summary>
    public string SalaryPeriod { get; set; } = null!;

    /// <summary>Employment type: FullTime, PartTime, Contract, Temporary.</summary>
    public string EmploymentType { get; set; } = null!;

    /// <summary>Proposed start date.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Description of benefits package.</summary>
    public string? Benefits { get; set; }

    /// <summary>Any other terms or conditions.</summary>
    public string? AdditionalTerms { get; set; }

    /// <summary>CDN/blob storage URL to the formal offer letter document.</summary>
    public string? OfferLetterUrl { get; set; }

    /// <summary>Offer expiration deadline.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Ref to <c>auth.users.id</c> (cross-module) — who extended the offer.</summary>
    public Guid ExtendedBy { get; set; }

    /// <summary>When the offer was sent to the candidate (Draft → Pending).</summary>
    public DateTime? ExtendedAt { get; set; }

    /// <summary>When the candidate accepted or declined.</summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>Why the candidate turned it down (optional).</summary>
    public string? DeclineReason { get; set; }

    /// <summary>When the company withdrew the offer.</summary>
    public DateTime? WithdrawnAt { get; set; }

    /// <summary>Why the offer was withdrawn.</summary>
    public string? WithdrawalReason { get; set; }
}
