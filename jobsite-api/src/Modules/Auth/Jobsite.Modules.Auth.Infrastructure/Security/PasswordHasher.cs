using Jobsite.Modules.Auth.Application.Interfaces;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// BCrypt password hashing implementation.
/// Uses BCrypt's built-in salt generation and constant-time comparison.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
