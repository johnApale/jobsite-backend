namespace Jobsite.Modules.HRWorkflows.Application.DTOs;

// ── Requests ─────────────────────────────────────────────────────────────

public sealed class CreateOfferRequest
{
    public required Guid ApplicationId { get; init; }
    public required decimal Salary { get; init; }
    public required string SalaryCurrency { get; init; }
    public required string SalaryPeriod { get; init; }
    public required string EmploymentType { get; init; }
    public DateTime? StartDate { get; init; }
    public string? Benefits { get; init; }
    public string? AdditionalTerms { get; init; }
    public string? OfferLetterUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public Guid? ClientCompanyId { get; init; }
}

public sealed class UpdateOfferRequest
{
    public decimal? Salary { get; init; }
    public string? SalaryCurrency { get; init; }
    public string? SalaryPeriod { get; init; }
    public string? EmploymentType { get; init; }
    public DateTime? StartDate { get; init; }
    public string? Benefits { get; init; }
    public string? AdditionalTerms { get; init; }
    public string? OfferLetterUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed class RespondToOfferRequest
{
    public required bool Accepted { get; init; }
    public string? DeclineReason { get; init; }
}

public sealed class WithdrawOfferRequest
{
    public string? WithdrawalReason { get; init; }
}

// ── Responses ────────────────────────────────────────────────────────────

public sealed class JobOfferResponse
{
    public required Guid ApplicationId { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public required string Status { get; init; }
    public required decimal Salary { get; init; }
    public required string SalaryCurrency { get; init; }
    public required string SalaryPeriod { get; init; }
    public required string EmploymentType { get; init; }
    public DateTime? StartDate { get; init; }
    public string? Benefits { get; init; }
    public string? AdditionalTerms { get; init; }
    public string? OfferLetterUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public required Guid ExtendedBy { get; init; }
    public DateTime? ExtendedAt { get; init; }
    public DateTime? RespondedAt { get; init; }
    public string? DeclineReason { get; init; }
    public DateTime? WithdrawnAt { get; init; }
    public string? WithdrawalReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class OfferListResponse
{
    public required List<JobOfferResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed class OfferQueryParameters
{
    public string? Status { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}
