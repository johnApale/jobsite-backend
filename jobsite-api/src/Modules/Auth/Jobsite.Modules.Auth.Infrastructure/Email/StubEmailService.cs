using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Auth.Infrastructure.Email;

/// <summary>
/// Stub email service that logs emails instead of sending them.
/// Replace with a real SMTP/SendGrid implementation in production.
/// </summary>
public sealed class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;
    private readonly IHostEnvironment _environment;

    public StubEmailService(ILogger<StubEmailService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task SendVerificationEmailAsync(string email, string token, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "StubEmailService is active in {Environment}. Replace with a real email provider",
                _environment.EnvironmentName);
        }

        _logger.LogInformation(
            "Stub: Verification email for {Email} — token: {Token}",
            email, token);

        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string token, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "StubEmailService is active in {Environment}. Replace with a real email provider",
                _environment.EnvironmentName);
        }

        _logger.LogInformation(
            "Stub: Password reset email for {Email} — token: {Token}",
            email, token);

        return Task.CompletedTask;
    }
}
