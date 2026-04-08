using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using System.Security.Cryptography;

namespace Jobsite.Modules.Auth.Application.Services;

/// <summary>
/// Application service for authentication: register, login, token refresh, OAuth, logout,
/// email verification, and password reset.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IOAuthProviderValidator _oauthValidator;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtSettings _settings;

    public AuthService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IOAuthProviderValidator oauthValidator,
        IEmailService emailService,
        JwtSettings settings,
        [FromKeyedServices("auth")] IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _oauthValidator = oauthValidator;
        _emailService = emailService;
        _settings = settings;
        _unitOfWork = unitOfWork;
    }

    public async Task<AuthTokensResponse> RegisterAsync(RegisterRequest request, Guid tenantId, CancellationToken ct = default)
    {
        bool emailExists = await _userRepository.EmailExistsAsync(request.Email, ct);
        if (emailExists)
            throw AppErrors.DuplicateEmail;

        string role = request.Role ?? UserRole.Applicant;
        string verificationToken = GenerateSecureToken();

        User user = new()
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            EmailVerified = false,
            Role = role,
            Status = UserStatus.Active,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(_settings.EmailVerificationTokenExpirationHours),
        };

        user.Raise(new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            RegisteredAt = DateTime.UtcNow
        });

        _userRepository.Add(user);
        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, verificationToken, ct);

        return await IssueTokensAsync(user, tenantId, ct);
    }

    public async Task<AuthTokensResponse> LoginAsync(LoginRequest request, Guid tenantId, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByEmailForUpdateAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            throw AppErrors.InvalidCredentials;

        if (user.Status == UserStatus.Deactivated)
            throw AppErrors.InvalidCredentials;

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            throw AppErrors.AccountLocked;

        // Clear expired lockout
        if (user.LockedUntil.HasValue && user.LockedUntil.Value <= DateTime.UtcNow)
        {
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
        }

        if (user.PasswordHash is null)
            throw AppErrors.InvalidCredentials;

        bool passwordValid = _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!passwordValid)
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= _settings.MaxFailedLoginAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(_settings.LockoutDurationMinutes);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            throw AppErrors.InvalidCredentials;
        }

        // Successful login: reset lockout counters
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, tenantId, ct);
    }

    public async Task<AuthTokensResponse> RefreshTokenAsync(RefreshTokenRequest request, Guid tenantId, CancellationToken ct = default)
    {
        string tokenHash = _jwtService.HashToken(request.RefreshToken);

        RefreshToken? existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);
        if (existingToken is null)
            throw AppErrors.InvalidCredentials;

        // Replay detection: if token is already revoked, revoke the entire family
        if (existingToken.IsRevoked)
        {
            await _refreshTokenRepository.RevokeFamilyAsync(existingToken.FamilyId, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            throw AppErrors.TokenReplayDetected;
        }

        if (existingToken.IsExpired)
            throw AppErrors.TokenExpired;

        // Rotate: revoke old token, create new one in the same family
        existingToken.Revoke();

        User? user = await _userRepository.GetByIdAsync(existingToken.UserId, ct);
        if (user is null)
            throw AppErrors.UserNotFound;

        if (user.Status == UserStatus.Deactivated)
            throw AppErrors.InvalidCredentials;

        string newRawToken = _jwtService.GenerateRefreshToken();
        string newTokenHash = _jwtService.HashToken(newRawToken);

        RefreshToken newToken = new()
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            FamilyId = existingToken.FamilyId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtService.RefreshTokenExpirationDays),
        };

        existingToken.ReplacedById = newToken.Id;

        _refreshTokenRepository.Add(newToken);
        await _unitOfWork.SaveChangesAsync(ct);

        string accessToken = _jwtService.GenerateAccessToken(user, tenantId);

        return new AuthTokensResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRawToken,
            ExpiresIn = _jwtService.AccessTokenExpirationMinutes * 60,
        };
    }

    public async Task<AuthTokensResponse> OAuthLoginAsync(string provider, OAuthLoginRequest request, Guid tenantId, CancellationToken ct = default)
    {
        if (!ExternalLoginProvider.IsValid(provider))
            throw AppErrors.InvalidRequest.WithMessage($"Unsupported OAuth provider: {provider}");

        OAuthUserInfo providerInfo = await _oauthValidator.ValidateTokenAsync(provider, request.ProviderToken, ct);

        // Use the email from the request if the validator returned empty (stub behavior)
        string email = string.IsNullOrEmpty(providerInfo.Email) ? request.Email : providerInfo.Email;

        // Case A: existing linked account
        User? user = await _userRepository.GetByExternalLoginAsync(provider, providerInfo.SubjectId, ct);

        if (user is not null)
        {
            if (user.Status == UserStatus.Deactivated)
                throw AppErrors.InvalidCredentials;

            User? trackedUser = await _userRepository.GetByIdForUpdateAsync(user.Id, ct);
            trackedUser!.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            return await IssueTokensAsync(user, tenantId, ct);
        }

        // Case B: no linked account, but email matches an existing user
        User? existingUser = await _userRepository.GetByEmailForUpdateAsync(email.ToLowerInvariant(), ct);

        if (existingUser is not null)
        {
            if (existingUser.Status == UserStatus.Deactivated)
                throw AppErrors.InvalidCredentials;

            existingUser.ExternalLogins.Add(new UserExternalLogin
            {
                UserId = existingUser.Id,
                Provider = provider,
                ProviderSubjectId = providerInfo.SubjectId,
                ProviderEmail = email,
                ProviderDisplayName = request.DisplayName ?? providerInfo.DisplayName,
                LinkedAt = DateTime.UtcNow,
            });

            if (providerInfo.EmailVerified)
                existingUser.EmailVerified = true;

            existingUser.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            return await IssueTokensAsync(existingUser, tenantId, ct);
        }

        // Case C: completely new user
        string displayName = request.DisplayName ?? providerInfo.DisplayName ?? "";
        string[] nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string firstName = nameParts.Length > 0 ? nameParts[0] : "User";
        string lastName = nameParts.Length > 1 ? nameParts[1] : "";

        User newUser = new()
        {
            Email = email.ToLowerInvariant(),
            EmailVerified = providerInfo.EmailVerified,
            Role = UserRole.Applicant,
            Status = UserStatus.Active,
            FirstName = firstName,
            LastName = lastName,
            ExternalLogins =
            [
                new UserExternalLogin
                {
                    Provider = provider,
                    ProviderSubjectId = providerInfo.SubjectId,
                    ProviderEmail = email,
                    ProviderDisplayName = request.DisplayName ?? providerInfo.DisplayName,
                    LinkedAt = DateTime.UtcNow,
                }
            ]
        };

        newUser.Raise(new UserRegisteredEvent
        {
            UserId = newUser.Id,
            Email = newUser.Email,
            Role = newUser.Role,
            RegisteredAt = DateTime.UtcNow
        });

        _userRepository.Add(newUser);
        await _unitOfWork.SaveChangesAsync(ct);

        return await IssueTokensAsync(newUser, tenantId, ct);
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        string tokenHash = _jwtService.HashToken(request.RefreshToken);

        RefreshToken? token = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, ct);
        if (token is null)
            return; // Idempotent — already logged out or invalid token

        if (!token.IsRevoked)
        {
            token.Revoke();
            await _unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            throw AppErrors.UserNotFound;

        return MapToResponse(user);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByEmailForUpdateAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            throw AppErrors.InvalidVerificationToken;

        if (user.EmailVerified)
            throw AppErrors.EmailAlreadyVerified;

        if (user.EmailVerificationToken is null
            || user.EmailVerificationToken != request.Token
            || user.EmailVerificationTokenExpiresAt is null
            || user.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
        {
            throw AppErrors.InvalidVerificationToken;
        }

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiresAt = null;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ResendVerificationEmailAsync(ResendVerificationRequest request, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByEmailForUpdateAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            return; // Silent — don't reveal whether email exists

        if (user.EmailVerified)
            return; // Already verified, nothing to do

        string verificationToken = GenerateSecureToken();
        user.EmailVerificationToken = verificationToken;
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(_settings.EmailVerificationTokenExpirationHours);
        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendVerificationEmailAsync(user.Email, verificationToken, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByEmailForUpdateAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            return; // Silent — don't reveal whether email exists

        string resetToken = GenerateSecureToken();
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(_settings.PasswordResetTokenExpirationHours);
        await _unitOfWork.SaveChangesAsync(ct);

        await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken, ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        User? user = await _userRepository.GetByEmailForUpdateAsync(request.Email.ToLowerInvariant(), ct);
        if (user is null)
            throw AppErrors.InvalidResetToken;

        if (user.PasswordResetToken is null
            || user.PasswordResetToken != request.Token
            || user.PasswordResetTokenExpiresAt is null
            || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
        {
            throw AppErrors.InvalidResetToken;
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(User user, Guid tenantId, CancellationToken ct)
    {
        string accessToken = _jwtService.GenerateAccessToken(user, tenantId);
        string rawRefreshToken = _jwtService.GenerateRefreshToken();
        string refreshTokenHash = _jwtService.HashToken(rawRefreshToken);

        RefreshToken refreshToken = new()
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            FamilyId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtService.RefreshTokenExpirationDays),
        };

        _refreshTokenRepository.Add(refreshToken);
        await _unitOfWork.SaveChangesAsync(ct);

        return new AuthTokensResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresIn = _jwtService.AccessTokenExpirationMinutes * 60,
        };
    }

    private static UserResponse MapToResponse(User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            Status = user.Status,
            EmailVerified = user.EmailVerified,
            AvatarUrl = user.AvatarUrl,
            CreatedAt = user.CreatedAt,
        };
    }

    private static string GenerateSecureToken()
    {
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes);
    }
}
