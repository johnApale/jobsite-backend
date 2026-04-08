namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Sends transactional emails for the Auth module.
/// Initially stubbed — real SMTP/SendGrid integration is deferred.
/// </summary>
public interface IEmailService
{
    /// <summary>Send an email verification link to the user.</summary>
    Task SendVerificationEmailAsync(string email, string token, CancellationToken ct = default);

    /// <summary>Send a password reset link to the user.</summary>
    Task SendPasswordResetEmailAsync(string email, string token, CancellationToken ct = default);
}
