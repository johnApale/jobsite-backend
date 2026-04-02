namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/login</c>.</summary>
public sealed class LoginRequest
{
    /// <summary>Login email address.</summary>
    public required string Email { get; init; }

    /// <summary>Password.</summary>
    public required string Password { get; init; }
}
