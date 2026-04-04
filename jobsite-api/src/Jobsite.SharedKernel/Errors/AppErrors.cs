namespace Jobsite.SharedKernel.Errors;

/// <summary>
/// Sentinel error instances for all known application errors.
/// Usage: <c>throw AppErrors.UserNotFound;</c> or <c>throw AppErrors.UserNotFound.WithMessage("...");</c>
/// </summary>
public static class AppErrors
{
    // ── 400 Bad Request ──────────────────────────────────────────────────
    public static AppError Validation => new("VALIDATION_ERROR", 400, "Request validation failed");
    public static AppError InvalidRequest => new("INVALID_REQUEST", 400, "Structurally invalid request");
    public static AppError DuplicateEmail => new("DUPLICATE_EMAIL", 400, "Email already registered for this tenant");
    public static AppError DuplicateApplication => new("DUPLICATE_APPLICATION", 400, "Applicant already applied to this job posting");

    // ── 401 Unauthorized ─────────────────────────────────────────────────
    public static AppError Unauthorized => new("UNAUTHORIZED", 401, "Missing or invalid authentication");
    public static AppError InvalidCredentials => new("INVALID_CREDENTIALS", 401, "Wrong email or password");
    public static AppError TokenExpired => new("TOKEN_EXPIRED", 401, "Token has expired");
    public static AppError TokenReplayDetected => new("TOKEN_REPLAY_DETECTED", 401, "Refresh token reuse detected");

    // ── 403 Forbidden ────────────────────────────────────────────────────
    public static AppError Forbidden => new("FORBIDDEN", 403, "You do not have permission to access this resource");

    // ── 404 Not Found ────────────────────────────────────────────────────
    public static AppError TenantNotFound => new("TENANT_NOT_FOUND", 404, "Tenant not found");
    public static AppError UserNotFound => new("USER_NOT_FOUND", 404, "User not found");
    public static AppError JobPostingNotFound => new("JOB_POSTING_NOT_FOUND", 404, "Job posting not found");
    public static AppError ApplicationNotFound => new("APPLICATION_NOT_FOUND", 404, "Application not found");
    public static AppError ClientCompanyNotFound => new("CLIENT_COMPANY_NOT_FOUND", 404, "Client company not found");
    public static AppError CriteriaNotFound => new("CRITERIA_NOT_FOUND", 404, "Evaluation criteria not found");
    public static AppError ScreeningQuestionNotFound => new("SCREENING_QUESTION_NOT_FOUND", 404, "Screening question not found");
    public static AppError ProfileNotFound => new("PROFILE_NOT_FOUND", 404, "Profile not found");
    public static AppError ResumeNotFound => new("RESUME_NOT_FOUND", 404, "Resume not found");
    public static AppError SettingsNotFound => new("SETTINGS_NOT_FOUND", 404, "Company settings not found for this tenant");
    public static AppError ScreeningResultNotFound => new("SCREENING_RESULT_NOT_FOUND", 404, "Screening result not found");

    // ── 409 Conflict ─────────────────────────────────────────────────────
    public static AppError ProfileAlreadyExists => new("PROFILE_ALREADY_EXISTS", 409, "Profile already exists for this user");
    public static AppError ApplicationAlreadyWithdrawn => new("APPLICATION_ALREADY_WITHDRAWN", 409, "Application has already been withdrawn");
    public static AppError OfferAlreadyAccepted => new("OFFER_ALREADY_ACCEPTED", 409, "Offer has already been accepted");
    public static AppError ScreeningAlreadyCompleted => new("SCREENING_ALREADY_COMPLETED", 409, "Screening has already been completed for this application");
    public static AppError AssessmentAlreadySubmitted => new("ASSESSMENT_ALREADY_SUBMITTED", 409, "Assessment answers have already been submitted for this application");

    // ── 422 Unprocessable Entity ─────────────────────────────────────────
    public static AppError UnprocessableEntity => new("UNPROCESSABLE_ENTITY", 422, "Business logic prevents this operation");
    public static AppError AssessmentNotAvailable => new("ASSESSMENT_NOT_AVAILABLE", 422, "Assessment is not available for this application");

    // ── 429 Too Many Requests ────────────────────────────────────────────
    public static AppError RateLimited => new("RATE_LIMITED", 429, "Rate limit exceeded");

    // ── 500 Internal Server Error ────────────────────────────────────────
    public static AppError InternalError => new("INTERNAL_ERROR", 500, "An unexpected error occurred");

    // ── 503 Service Unavailable ──────────────────────────────────────────
    public static AppError ServiceUnavailable => new("SERVICE_UNAVAILABLE", 503, "A required service is currently unavailable");
}
