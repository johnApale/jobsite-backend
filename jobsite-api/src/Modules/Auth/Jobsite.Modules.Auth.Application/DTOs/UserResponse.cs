namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Response body for authenticated user profile.</summary>
public sealed class UserResponse
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public required bool EmailVerified { get; init; }
    public string? AvatarUrl { get; init; }
    public required DateTime CreatedAt { get; init; }
}
