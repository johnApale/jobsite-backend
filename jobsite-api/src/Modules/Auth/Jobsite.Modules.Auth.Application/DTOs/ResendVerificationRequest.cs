namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/resend-verification</c>.</summary>
public sealed class ResendVerificationRequest
{
    /// <summary>The user's email address to resend verification for.</summary>
    public required string Email { get; init; }
}
