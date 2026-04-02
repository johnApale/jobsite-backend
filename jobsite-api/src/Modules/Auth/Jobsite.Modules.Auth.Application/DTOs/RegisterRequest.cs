namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/register</c>.</summary>
public sealed class RegisterRequest
{
    /// <summary>Login email address.</summary>
    public required string Email { get; init; }

    /// <summary>Password (min 8 characters).</summary>
    public required string Password { get; init; }

    /// <summary>User's first name.</summary>
    public required string FirstName { get; init; }

    /// <summary>User's last name.</summary>
    public required string LastName { get; init; }

    /// <summary>Role (defaults to Applicant if omitted).</summary>
    public string? Role { get; init; }
}
