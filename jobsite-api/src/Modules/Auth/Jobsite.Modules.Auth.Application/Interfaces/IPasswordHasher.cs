namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Abstraction for password hashing and verification.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hash a plaintext password using BCrypt.</summary>
    string HashPassword(string password);

    /// <summary>Verify a plaintext password against a BCrypt hash.</summary>
    bool VerifyPassword(string password, string hash);
}
