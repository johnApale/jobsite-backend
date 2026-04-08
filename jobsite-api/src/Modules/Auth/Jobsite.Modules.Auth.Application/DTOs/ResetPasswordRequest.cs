namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/reset-password</c>.</summary>
public sealed class ResetPasswordRequest
{
    /// <summary>The user's email address.</summary>
    public required string Email { get; init; }

    /// <summary>The password reset token received via email.</summary>
    public required string Token { get; init; }

    /// <summary>The new password (min 8 characters).</summary>
    public required string NewPassword { get; init; }
}
