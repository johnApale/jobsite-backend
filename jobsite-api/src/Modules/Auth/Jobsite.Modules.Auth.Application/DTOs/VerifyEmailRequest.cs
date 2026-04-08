namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/verify-email</c>.</summary>
public sealed class VerifyEmailRequest
{
    /// <summary>The user's email address.</summary>
    public required string Email { get; init; }

    /// <summary>The email verification token.</summary>
    public required string Token { get; init; }
}
