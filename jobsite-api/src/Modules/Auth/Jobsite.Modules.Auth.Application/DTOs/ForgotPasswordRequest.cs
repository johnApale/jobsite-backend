namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/forgot-password</c>.</summary>
public sealed class ForgotPasswordRequest
{
    /// <summary>The email address to send the password reset token to.</summary>
    public required string Email { get; init; }
}
