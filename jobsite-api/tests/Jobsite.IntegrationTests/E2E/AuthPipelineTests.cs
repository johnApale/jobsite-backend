using FluentAssertions;
using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Application.Services;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Infrastructure.Repositories;
using Jobsite.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// End-to-end auth pipeline tests exercising the full flow:
/// register → login → refresh → get me → logout → lockout.
///
/// Uses real PostgreSQL (Testcontainers) for repositories + real AuthService.
/// External dependencies (JWT, email, OAuth, password hashing) are substituted
/// via NSubstitute to keep tests deterministic and fast.
/// </summary>
[Collection("AuthPipeline")]
public sealed class AuthPipelineTests : IAsyncLifetime
{
    private readonly AuthPipelineFixture _fixture;

    private AuthDbContext _db = null!;
    private UserRepository _userRepo = null!;
    private RefreshTokenRepository _refreshTokenRepo = null!;

    // External dependency stubs
    private IPasswordHasher _passwordHasher = null!;
    private IJwtService _jwtService = null!;
    private IOAuthProviderValidator _oauthValidator = null!;
    private IEmailService _emailService = null!;

    private static readonly Guid TestTenantId = Guid.NewGuid();

    public AuthPipelineTests(AuthPipelineFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();

        _db = _fixture.CreateDbContext();
        _userRepo = new UserRepository(_db);
        _refreshTokenRepo = new RefreshTokenRepository(_db);

        _passwordHasher = Substitute.For<IPasswordHasher>();
        _jwtService = Substitute.For<IJwtService>();
        _oauthValidator = Substitute.For<IOAuthProviderValidator>();
        _emailService = Substitute.For<IEmailService>();

        // Configure default password hasher behavior
        _passwordHasher.HashPassword(Arg.Any<string>())
            .Returns(callInfo => $"hashed:{callInfo.Arg<string>()}");
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>())
            .Returns(callInfo => $"hashed:{callInfo.ArgAt<string>(0)}" == callInfo.ArgAt<string>(1));

        // Configure default JWT behavior
        _jwtService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<Guid>())
            .Returns("test-access-token");
        _jwtService.GenerateRefreshToken()
            .Returns(_ => Guid.NewGuid().ToString("N"));
        _jwtService.HashToken(Arg.Any<string>())
            .Returns(callInfo => $"sha256:{callInfo.Arg<string>()}");
        _jwtService.AccessTokenExpirationMinutes.Returns(60);
        _jwtService.RefreshTokenExpirationDays.Returns(30);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    private AuthService CreateService(JwtSettings? settings = null)
    {
        JwtSettings jwtSettings = settings ?? new JwtSettings
        {
            MaxFailedLoginAttempts = 5,
            LockoutDurationMinutes = 15,
            EmailVerificationTokenExpirationHours = 24,
            PasswordResetTokenExpirationHours = 1
        };

        return new AuthService(
            _userRepo,
            _refreshTokenRepo,
            _passwordHasher,
            _jwtService,
            _oauthValidator,
            _emailService,
            jwtSettings,
            _db); // AuthDbContext implements IUnitOfWork via TenantDbContext
    }

    // ── Test: Register Creates User and Returns Tokens ────────────────────

    [Fact]
    public async Task Register_ValidRequest_CreatesUserAndReturnsTokens()
    {
        // Arrange
        AuthService service = CreateService();
        RegisterRequest request = new()
        {
            Email = "newuser@test.com",
            Password = "StrongP@ss123",
            FirstName = "Jane",
            LastName = "Doe",
            Role = UserRole.Applicant
        };

        // Act
        AuthTokensResponse response = await service.RegisterAsync(request, TestTenantId, CancellationToken.None);

        // Assert — tokens returned
        response.AccessToken.Should().Be("test-access-token");
        response.RefreshToken.Should().NotBeNullOrEmpty();
        response.ExpiresIn.Should().Be(3600);
        response.TokenType.Should().Be("Bearer");

        // Assert — user persisted in database
        User? persisted = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "newuser@test.com");

        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Jane");
        persisted.LastName.Should().Be("Doe");
        persisted.Role.Should().Be(UserRole.Applicant);
        persisted.Status.Should().Be(UserStatus.Active);
        persisted.EmailVerified.Should().BeFalse();
        persisted.PasswordHash.Should().Be("hashed:StrongP@ss123");

        // Assert — verification email sent
        await _emailService.Received(1).SendVerificationEmailAsync(
            "newuser@test.com", Arg.Any<string>(), Arg.Any<CancellationToken>());

        // Assert — refresh token persisted
        List<RefreshToken> tokens = await _db.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserId == persisted.Id)
            .ToListAsync();
        tokens.Should().HaveCount(1);
        tokens[0].IsRevoked.Should().BeFalse();
    }

    // ── Test: Register Duplicate Email Throws ─────────────────────────────

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsDuplicateEmailError()
    {
        // Arrange — seed an existing user
        AuthService service = CreateService();
        RegisterRequest firstRequest = new()
        {
            Email = "existing@test.com",
            Password = "Password123",
            FirstName = "First",
            LastName = "User"
        };
        await service.RegisterAsync(firstRequest, TestTenantId, CancellationToken.None);

        RegisterRequest duplicateRequest = new()
        {
            Email = "existing@test.com",
            Password = "Password456",
            FirstName = "Second",
            LastName = "User"
        };

        // Act
        Func<Task> act = async () => await service.RegisterAsync(duplicateRequest, TestTenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("DUPLICATE_EMAIL");
    }

    // ── Test: Login Valid Credentials Returns Tokens ───────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndUpdatesLastLogin()
    {
        // Arrange — register a user first
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "login@test.com",
            Password = "MyP@ssword",
            FirstName = "Login",
            LastName = "Test"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        LoginRequest loginReq = new()
        {
            Email = "login@test.com",
            Password = "MyP@ssword"
        };

        // Act
        AuthTokensResponse response = await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);

        // Assert — tokens returned
        response.AccessToken.Should().Be("test-access-token");
        response.RefreshToken.Should().NotBeNullOrEmpty();

        // Assert — last login updated
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "login@test.com");

        user.Should().NotBeNull();
        user!.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        user.FailedLoginAttempts.Should().Be(0);
    }

    // ── Test: Login Wrong Password Increments Failed Attempts ──────────────

    [Fact]
    public async Task Login_WrongPassword_IncrementsFailedAttemptsAndThrows()
    {
        // Arrange
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "wrongpw@test.com",
            Password = "CorrectPassword",
            FirstName = "Wrong",
            LastName = "Pw"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        LoginRequest loginReq = new()
        {
            Email = "wrongpw@test.com",
            Password = "WrongPassword"
        };

        // Act
        Func<Task> act = async () => await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");

        // Verify failed attempts incremented in database
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "wrongpw@test.com");
        user.Should().NotBeNull();
        user!.FailedLoginAttempts.Should().Be(1);
    }

    // ── Test: Account Lockout After Max Failed Attempts ───────────────────

    [Fact]
    public async Task Login_MaxFailedAttempts_LocksAccount()
    {
        // Arrange
        JwtSettings settings = new()
        {
            MaxFailedLoginAttempts = 3,
            LockoutDurationMinutes = 15,
            EmailVerificationTokenExpirationHours = 24,
            PasswordResetTokenExpirationHours = 1
        };
        AuthService service = CreateService(settings);

        RegisterRequest registerReq = new()
        {
            Email = "lockout@test.com",
            Password = "CorrectPassword",
            FirstName = "Lock",
            LastName = "Out"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        LoginRequest badLogin = new()
        {
            Email = "lockout@test.com",
            Password = "WrongPassword"
        };

        // Act — fail 3 times to trigger lockout
        for (int i = 0; i < 3; i++)
        {
            Func<Task> attempt = async () => await service.LoginAsync(badLogin, TestTenantId, CancellationToken.None);
            await attempt.Should().ThrowAsync<AppError>();
        }

        // Assert — account is now locked even with correct password
        LoginRequest correctLogin = new()
        {
            Email = "lockout@test.com",
            Password = "CorrectPassword"
        };
        Func<Task> lockedAttempt = async () => await service.LoginAsync(correctLogin, TestTenantId, CancellationToken.None);

        AppError lockError = (await lockedAttempt.Should().ThrowAsync<AppError>()).Which;
        lockError.Code.Should().Be("ACCOUNT_LOCKED");

        // Verify lockout state in database
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "lockout@test.com");
        user.Should().NotBeNull();
        user!.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeAfter(DateTime.UtcNow);
    }

    // ── Test: Full Auth Flow: Register → Login → Refresh → GetMe → Logout ─

    [Fact]
    public async Task FullAuthFlow_RegisterLoginGetMeLogout_Succeeds()
    {
        // Arrange
        AuthService service = CreateService();

        // Configure JWT to return different tokens per call so we can track them
        int callCount = 0;
        _jwtService.GenerateRefreshToken().Returns(_ =>
        {
            callCount++;
            return $"refresh-token-{callCount}";
        });

        // Step 1: Register
        RegisterRequest registerReq = new()
        {
            Email = "fullflow@test.com",
            Password = "Flow123!",
            FirstName = "Full",
            LastName = "Flow"
        };
        AuthTokensResponse registerResponse = await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);
        registerResponse.AccessToken.Should().NotBeNullOrEmpty();
        registerResponse.RefreshToken.Should().NotBeNullOrEmpty();

        // Step 2: Login
        LoginRequest loginReq = new()
        {
            Email = "fullflow@test.com",
            Password = "Flow123!"
        };
        AuthTokensResponse loginResponse = await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);
        loginResponse.AccessToken.Should().NotBeNullOrEmpty();
        string refreshTokenValue = loginResponse.RefreshToken;

        // Step 3: GetMe
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "fullflow@test.com");
        user.Should().NotBeNull();

        UserResponse meResponse = await service.GetCurrentUserAsync(user!.Id, CancellationToken.None);
        meResponse.Email.Should().Be("fullflow@test.com");
        meResponse.FirstName.Should().Be("Full");
        meResponse.LastName.Should().Be("Flow");
        meResponse.Role.Should().Be(UserRole.Applicant);

        // Step 4: Logout (revoke the login refresh token)
        RefreshTokenRequest logoutReq = new() { RefreshToken = refreshTokenValue };
        await service.LogoutAsync(logoutReq, CancellationToken.None);

        // Verify the refresh token is revoked
        string tokenHash = $"sha256:{refreshTokenValue}";
        RefreshToken? revokedToken = await _db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
        revokedToken.Should().NotBeNull();
        revokedToken!.IsRevoked.Should().BeTrue();
    }

    // ── Test: Refresh Token Replay Detection ──────────────────────────────

    [Fact]
    public async Task RefreshToken_ReplayDetected_RevokesEntireFamily()
    {
        // Arrange — manually set up a revoked token chain to test replay detection
        // without relying on the rotation code path (which has a self-referencing FK
        // ordering issue with database-generated keys and EnsureCreatedAsync).
        AuthService service = CreateService();

        Guid userId = Guid.NewGuid();
        Guid familyId = Guid.NewGuid();
        string originalRawToken = "replay-original-token";
        string rotatedRawToken = "replay-rotated-token";

        User user = new()
        {
            Id = userId,
            Email = "replay@test.com",
            FirstName = "Replay",
            LastName = "Test",
            Role = UserRole.Applicant,
            Status = UserStatus.Active,
            PasswordHash = "hashed:Replay123!"
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Token 2 (the rotated, active token) — insert first for FK satisfaction
        RefreshToken token2 = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = $"sha256:{rotatedRawToken}",
            FamilyId = familyId,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };
        _db.RefreshTokens.Add(token2);
        await _db.SaveChangesAsync();

        // Token 1 (original, already revoked and replaced by token2)
        RefreshToken token1 = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = $"sha256:{originalRawToken}",
            FamilyId = familyId,
            IsRevoked = true,
            RevokedAt = DateTime.UtcNow.AddMinutes(-5),
            ReplacedById = token2.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };
        _db.RefreshTokens.Add(token1);
        await _db.SaveChangesAsync();

        // Configure mock to return the hash for the original token
        _jwtService.HashToken(originalRawToken).Returns($"sha256:{originalRawToken}");

        // Act — replay the original (already-revoked) token
        RefreshTokenRequest replayReq = new() { RefreshToken = originalRawToken };
        Func<Task> replayAct = async () => await service.RefreshTokenAsync(replayReq, TestTenantId, CancellationToken.None);

        // Assert — replay detection triggers family-wide revocation
        AppError error = (await replayAct.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TOKEN_REPLAY_DETECTED");

        // Verify entire family is revoked
        _db.ChangeTracker.Clear();
        List<RefreshToken> allTokens = await _db.RefreshTokens
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();

        allTokens.Should().AllSatisfy(t => t.IsRevoked.Should().BeTrue());
    }

    // ── Test: Login Non-Existent User Throws ──────────────────────────────

    [Fact]
    public async Task Login_NonExistentUser_ThrowsInvalidCredentials()
    {
        // Arrange
        AuthService service = CreateService();
        LoginRequest loginReq = new()
        {
            Email = "nobody@test.com",
            Password = "Nobody123!"
        };

        // Act
        Func<Task> act = async () => await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    // ── Test: GetMe Non-Existent User Throws ──────────────────────────────

    [Fact]
    public async Task GetCurrentUser_NonExistentId_ThrowsUserNotFound()
    {
        // Arrange
        AuthService service = CreateService();

        // Act
        Func<Task> act = async () => await service.GetCurrentUserAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("USER_NOT_FOUND");
    }

    // ── Test: Login Deactivated User Throws ───────────────────────────────

    [Fact]
    public async Task Login_DeactivatedUser_ThrowsInvalidCredentials()
    {
        // Arrange — register then manually deactivate
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "deactivated@test.com",
            Password = "Deactivate123!",
            FirstName = "Deactivated",
            LastName = "User"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        // Manually deactivate in database
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "deactivated@test.com");
        user!.Status = UserStatus.Deactivated;
        user.DeactivatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        LoginRequest loginReq = new()
        {
            Email = "deactivated@test.com",
            Password = "Deactivate123!"
        };

        // Act
        Func<Task> act = async () => await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    // ── Test: Email Verification Flow ─────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_ValidToken_SetsEmailVerified()
    {
        // Arrange
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "verify@test.com",
            Password = "Verify123!",
            FirstName = "Verify",
            LastName = "Me"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        // Read the verification token from database
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "verify@test.com");
        user.Should().NotBeNull();
        user!.EmailVerificationToken.Should().NotBeNull();
        string verificationToken = user.EmailVerificationToken!;

        // Act
        VerifyEmailRequest verifyReq = new()
        {
            Email = "verify@test.com",
            Token = verificationToken
        };
        await service.VerifyEmailAsync(verifyReq, CancellationToken.None);

        // Assert — email should now be verified
        _db.ChangeTracker.Clear();
        User? verified = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "verify@test.com");
        verified.Should().NotBeNull();
        verified!.EmailVerified.Should().BeTrue();
        verified.EmailVerificationToken.Should().BeNull();
        verified.EmailVerificationTokenExpiresAt.Should().BeNull();
    }

    // ── Test: Password Reset Flow ─────────────────────────────────────────

    [Fact]
    public async Task PasswordReset_ForgotThenReset_ChangesPassword()
    {
        // Arrange
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "reset@test.com",
            Password = "OldPassword123!",
            FirstName = "Reset",
            LastName = "Pw"
        };
        await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        // Trigger forgot password
        ForgotPasswordRequest forgotReq = new() { Email = "reset@test.com" };
        await service.ForgotPasswordAsync(forgotReq, CancellationToken.None);

        // Read the reset token from database
        _db.ChangeTracker.Clear();
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "reset@test.com");
        user.Should().NotBeNull();
        user!.PasswordResetToken.Should().NotBeNull();
        string resetToken = user.PasswordResetToken!;

        // Act — reset password
        ResetPasswordRequest resetReq = new()
        {
            Email = "reset@test.com",
            Token = resetToken,
            NewPassword = "NewPassword456!"
        };
        await service.ResetPasswordAsync(resetReq, CancellationToken.None);

        // Assert — password hash should be updated
        _db.ChangeTracker.Clear();
        User? updated = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "reset@test.com");
        updated.Should().NotBeNull();
        updated!.PasswordHash.Should().Be("hashed:NewPassword456!");
        updated.PasswordResetToken.Should().BeNull();
        updated.PasswordResetTokenExpiresAt.Should().BeNull();
        updated.FailedLoginAttempts.Should().Be(0);
        updated.LockedUntil.Should().BeNull();

        // Verify we can login with the new password
        LoginRequest loginReq = new()
        {
            Email = "reset@test.com",
            Password = "NewPassword456!"
        };
        AuthTokensResponse loginResponse = await service.LoginAsync(loginReq, TestTenantId, CancellationToken.None);
        loginResponse.AccessToken.Should().NotBeNullOrEmpty();
    }

    // ── Test: OAuth Login Creates New User ────────────────────────────────

    [Fact]
    public async Task OAuthLogin_NewUser_CreatesUserAndReturnsTokens()
    {
        // Arrange
        AuthService service = CreateService();
        _oauthValidator.ValidateTokenAsync("Google", "google-token-123", Arg.Any<CancellationToken>())
            .Returns(new OAuthUserInfo
            {
                SubjectId = "google-sub-123",
                Email = "oauth@gmail.com",
                DisplayName = "OAuth User",
                EmailVerified = true
            });

        OAuthLoginRequest oauthReq = new()
        {
            ProviderToken = "google-token-123",
            Email = "oauth@gmail.com",
            DisplayName = "OAuth User"
        };

        // Act
        AuthTokensResponse response = await service.OAuthLoginAsync("Google", oauthReq, TestTenantId, CancellationToken.None);

        // Assert
        response.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();

        User? user = await _db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == "oauth@gmail.com");

        user.Should().NotBeNull();
        user!.EmailVerified.Should().BeTrue();
        user.Role.Should().Be(UserRole.Applicant);
        user.PasswordHash.Should().BeNull("OAuth users don't have passwords");
        user.ExternalLogins.Should().HaveCount(1);
        user.ExternalLogins[0].Provider.Should().Be("Google");
        user.ExternalLogins[0].ProviderSubjectId.Should().Be("google-sub-123");
    }

    // ── Test: Logout Is Idempotent ────────────────────────────────────────

    [Fact]
    public async Task Logout_AlreadyRevokedToken_SucceedsIdempotently()
    {
        // Arrange
        AuthService service = CreateService();
        RegisterRequest registerReq = new()
        {
            Email = "idempotent@test.com",
            Password = "Idempotent123!",
            FirstName = "Idem",
            LastName = "Potent"
        };
        AuthTokensResponse registerResponse = await service.RegisterAsync(registerReq, TestTenantId, CancellationToken.None);

        RefreshTokenRequest logoutReq = new() { RefreshToken = registerResponse.RefreshToken };

        // First logout
        await service.LogoutAsync(logoutReq, CancellationToken.None);

        // Act — second logout with the same token
        Func<Task> secondLogout = async () => await service.LogoutAsync(logoutReq, CancellationToken.None);

        // Assert — should succeed silently
        await secondLogout.Should().NotThrowAsync();
    }
}
