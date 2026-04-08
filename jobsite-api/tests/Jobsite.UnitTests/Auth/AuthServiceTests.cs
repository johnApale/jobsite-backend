using FluentAssertions;
using Jobsite.Modules.Auth.Application.Configuration;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Application.Services;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="AuthService"/> application service.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtService _jwtService = Substitute.For<IJwtService>();
    private readonly IOAuthProviderValidator _oauthValidator = Substitute.For<IOAuthProviderValidator>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly JwtSettings _settings = new();
    private readonly AuthService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public AuthServiceTests()
    {
        _jwtService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<Guid>()).Returns("access-token");
        _jwtService.GenerateRefreshToken().Returns("refresh-token");
        _jwtService.HashToken(Arg.Any<string>()).Returns("hashed-token");
        _jwtService.AccessTokenExpirationMinutes.Returns(60);
        _jwtService.RefreshTokenExpirationDays.Returns(30);

        _sut = new AuthService(
            _userRepo, _refreshTokenRepo, _passwordHasher,
            _jwtService, _oauthValidator, _emailService, _settings, _unitOfWork);
    }

    // ── Register ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_ValidRequest_ReturnsTokens()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest();
        _userRepo.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hashed-password");

        // Act
        AuthTokensResponse result = await _sut.RegisterAsync(request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.ExpiresIn.Should().Be(3600);
        _userRepo.Received(1).Add(Arg.Any<User>());
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailError()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest();
        _userRepo.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        Func<Task> act = () => _sut.RegisterAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("DUPLICATE_EMAIL");
    }

    [Fact]
    public async Task RegisterAsync_NoRoleProvided_DefaultsToApplicant()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(role: null);
        _userRepo.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        await _sut.RegisterAsync(request, _tenantId, CancellationToken.None);

        // Assert
        _userRepo.Received(1).Add(Arg.Is<User>(u => u.Role == UserRole.Applicant));
    }

    [Fact]
    public async Task RegisterAsync_RaisesUserRegisteredEvent()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest();
        _userRepo.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        await _sut.RegisterAsync(request, _tenantId, CancellationToken.None);

        // Assert
        _userRepo.Received(1).Add(Arg.Is<User>(u => u.DomainEvents.Count == 1));
    }

    // ── Login ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser(email: "test@example.com");
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        AuthTokensResponse result = await _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsInvalidCredentials()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUser_ThrowsInvalidCredentials()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser(status: UserStatus.Deactivated);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task LoginAsync_NullPasswordHash_ThrowsInvalidCredentials()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser(passwordHash: null!);
        user.PasswordHash = null;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentials()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_UpdatesLastLoginAt()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        await _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    // ── RefreshToken ─────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "valid-refresh-token" };
        User user = TestData.CreateUser();
        RefreshToken existingToken = TestData.CreateRefreshToken(userId: user.Id);
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        AuthTokensResponse result = await _sut.RefreshTokenAsync(request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        _refreshTokenRepo.Received(1).Add(Arg.Any<RefreshToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenNotFound_ThrowsInvalidCredentials()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "unknown" };
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        // Act
        Func<Task> act = () => _sut.RefreshTokenAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_RevokesEntireFamily()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "reused-token" };
        Guid familyId = Guid.NewGuid();
        RefreshToken revokedToken = TestData.CreateRefreshToken(familyId: familyId, isRevoked: true);
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(revokedToken);

        // Act
        Func<Task> act = () => _sut.RefreshTokenAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TOKEN_REPLAY_DETECTED");
        await _refreshTokenRepo.Received(1).RevokeFamilyAsync(familyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ThrowsTokenExpired()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "expired-token" };
        RefreshToken expiredToken = TestData.CreateRefreshToken(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expiredToken);

        // Act
        Func<Task> act = () => _sut.RefreshTokenAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TOKEN_EXPIRED");
    }

    [Fact]
    public async Task RefreshTokenAsync_DeactivatedUser_ThrowsInvalidCredentials()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "token" };
        User user = TestData.CreateUser(status: UserStatus.Deactivated);
        RefreshToken token = TestData.CreateRefreshToken(userId: user.Id);
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Func<Task> act = () => _sut.RefreshTokenAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_CREDENTIALS");
    }

    // ── Logout ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LogoutAsync_ValidToken_RevokesIt()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "logout-token" };
        RefreshToken token = TestData.CreateRefreshToken();
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        // Act
        await _sut.LogoutAsync(request, CancellationToken.None);

        // Assert
        token.IsRevoked.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAsync_TokenNotFound_DoesNotThrow()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "nonexistent" };
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        // Act
        Func<Task> act = () => _sut.LogoutAsync(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedToken_DoesNotSaveAgain()
    {
        // Arrange
        RefreshTokenRequest request = new() { RefreshToken = "already-revoked" };
        RefreshToken token = TestData.CreateRefreshToken(isRevoked: true);
        _refreshTokenRepo.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(token);

        // Act
        await _sut.LogoutAsync(request, CancellationToken.None);

        // Assert
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── GetCurrentUser ───────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUserAsync_ExistingUser_ReturnsUserResponse()
    {
        // Arrange
        User user = TestData.CreateUser(email: "me@test.com", role: UserRole.Recruiter);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        UserResponse result = await _sut.GetCurrentUserAsync(user.Id, CancellationToken.None);

        // Assert
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("me@test.com");
        result.Role.Should().Be(UserRole.Recruiter);
    }

    [Fact]
    public async Task GetCurrentUserAsync_UserNotFound_ThrowsUserNotFound()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _userRepo.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        Func<Task> act = () => _sut.GetCurrentUserAsync(userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("USER_NOT_FOUND");
    }

    // ── OAuthLogin ───────────────────────────────────────────────────────

    [Fact]
    public async Task OAuthLoginAsync_InvalidProvider_ThrowsInvalidRequest()
    {
        // Arrange
        OAuthLoginRequest request = new() { ProviderToken = "token", Email = "test@test.com" };

        // Act
        Func<Task> act = () => _sut.OAuthLoginAsync("InvalidProvider", request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task OAuthLoginAsync_ExistingLinkedAccount_ReturnsTokens()
    {
        // Arrange
        OAuthLoginRequest request = new() { ProviderToken = "token", Email = "oauth@test.com" };
        User user = TestData.CreateUser(email: "oauth@test.com");
        OAuthUserInfo oauthInfo = new() { SubjectId = "goog-123", Email = "oauth@test.com", EmailVerified = true };
        _oauthValidator.ValidateTokenAsync("Google", "token", Arg.Any<CancellationToken>()).Returns(oauthInfo);
        _userRepo.GetByExternalLoginAsync("Google", "goog-123", Arg.Any<CancellationToken>()).Returns(user);
        _userRepo.GetByIdForUpdateAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        AuthTokensResponse result = await _sut.OAuthLoginAsync("Google", request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OAuthLoginAsync_NewUser_CreatesUserAndReturnsTokens()
    {
        // Arrange
        OAuthLoginRequest request = new() { ProviderToken = "token", Email = "new@oauth.com", DisplayName = "New User" };
        OAuthUserInfo oauthInfo = new() { SubjectId = "goog-456", Email = "new@oauth.com", EmailVerified = true };
        _oauthValidator.ValidateTokenAsync("Google", "token", Arg.Any<CancellationToken>()).Returns(oauthInfo);
        _userRepo.GetByExternalLoginAsync("Google", "goog-456", Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepo.GetByEmailForUpdateAsync("new@oauth.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        AuthTokensResponse result = await _sut.OAuthLoginAsync("Google", request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        _userRepo.Received(1).Add(Arg.Is<User>(u =>
            u.Email == "new@oauth.com" &&
            u.Role == UserRole.Applicant &&
            u.ExternalLogins.Count == 1));
    }

    // ── Account Lockout ──────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_LockedAccount_ThrowsAccountLocked()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task LoginAsync_ExpiredLockout_ClearsLockAndAllowsLogin()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        user.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
        user.FailedLoginAttempts = 5;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        AuthTokensResponse result = await _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsFailedAttempts()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        user.FailedLoginAttempts = 0;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>();
        user.FailedLoginAttempts.Should().Be(1);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_MaxFailedAttempts_LocksAccount()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        user.FailedLoginAttempts = 4; // One more failure = 5 = threshold
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>();
        user.FailedLoginAttempts.Should().Be(5);
        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedAttempts()
    {
        // Arrange
        LoginRequest request = TestData.CreateLoginRequest();
        User user = TestData.CreateUser();
        user.FailedLoginAttempts = 3;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        // Act
        await _sut.LoginAsync(request, _tenantId, CancellationToken.None);

        // Assert
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    // ── Email Verification ───────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_SetsEmailVerificationToken()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest();
        _userRepo.EmailExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _passwordHasher.HashPassword(Arg.Any<string>()).Returns("hash");

        // Act
        await _sut.RegisterAsync(request, _tenantId, CancellationToken.None);

        // Assert
        _userRepo.Received(1).Add(Arg.Is<User>(u =>
            u.EmailVerificationToken != null &&
            u.EmailVerificationTokenExpiresAt != null));
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_VerifiesEmail()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerificationToken = "valid-token";
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "valid-token" };

        // Act
        await _sut.VerifyEmailAsync(request, CancellationToken.None);

        // Assert
        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
        user.EmailVerificationTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmailAsync_UserNotFound_ThrowsInvalidVerificationToken()
    {
        // Arrange
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        VerifyEmailRequest request = new() { Email = "unknown@example.com", Token = "token" };

        // Act
        Func<Task> act = () => _sut.VerifyEmailAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_VERIFICATION_TOKEN");
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyVerified_ThrowsEmailAlreadyVerified()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerified = true;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "token" };

        // Act
        Func<Task> act = () => _sut.VerifyEmailAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("EMAIL_ALREADY_VERIFIED");
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ThrowsInvalidVerificationToken()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerificationToken = "expired-token";
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(-1);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "expired-token" };

        // Act
        Func<Task> act = () => _sut.VerifyEmailAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_VERIFICATION_TOKEN");
    }

    [Fact]
    public async Task VerifyEmailAsync_WrongToken_ThrowsInvalidVerificationToken()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerificationToken = "correct-token";
        user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "wrong-token" };

        // Act
        Func<Task> act = () => _sut.VerifyEmailAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_VERIFICATION_TOKEN");
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_UnverifiedUser_SendsEmail()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerified = false;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        ResendVerificationRequest request = new() { Email = "test@example.com" };

        // Act
        await _sut.ResendVerificationEmailAsync(request, CancellationToken.None);

        // Assert
        user.EmailVerificationToken.Should().NotBeNull();
        await _emailService.Received(1).SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_UserNotFound_SilentReturn()
    {
        // Arrange
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        ResendVerificationRequest request = new() { Email = "unknown@example.com" };

        // Act
        Func<Task> act = () => _sut.ResendVerificationEmailAsync(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResendVerificationEmailAsync_AlreadyVerified_SilentReturn()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.EmailVerified = true;
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        ResendVerificationRequest request = new() { Email = "test@example.com" };

        // Act
        await _sut.ResendVerificationEmailAsync(request, CancellationToken.None);

        // Assert
        await _emailService.DidNotReceive().SendVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Password Reset ───────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPasswordAsync_ExistingUser_SendsResetEmail()
    {
        // Arrange
        User user = TestData.CreateUser();
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        ForgotPasswordRequest request = new() { Email = "test@example.com" };

        // Act
        await _sut.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        user.PasswordResetToken.Should().NotBeNull();
        user.PasswordResetTokenExpiresAt.Should().NotBeNull();
        await _emailService.Received(1).SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPasswordAsync_UserNotFound_SilentReturn()
    {
        // Arrange
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        ForgotPasswordRequest request = new() { Email = "unknown@example.com" };

        // Act
        Func<Task> act = () => _sut.ForgotPasswordAsync(request, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        await _emailService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ResetsPassword()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.PasswordResetToken = "valid-reset-token";
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        user.FailedLoginAttempts = 3;
        user.LockedUntil = DateTime.UtcNow.AddMinutes(10);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.HashPassword("NewPassword123!").Returns("new-hash");
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "valid-reset-token", NewPassword = "NewPassword123!" };

        // Act
        await _sut.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        user.PasswordHash.Should().Be("new-hash");
        user.PasswordResetToken.Should().BeNull();
        user.PasswordResetTokenExpiresAt.Should().BeNull();
        user.FailedLoginAttempts.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsInvalidResetToken()
    {
        // Arrange
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        ResetPasswordRequest request = new() { Email = "unknown@example.com", Token = "token", NewPassword = "NewPass123!" };

        // Act
        Func<Task> act = () => _sut.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_RESET_TOKEN");
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ThrowsInvalidResetToken()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.PasswordResetToken = "expired-token";
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(-1);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "expired-token", NewPassword = "NewPass123!" };

        // Act
        Func<Task> act = () => _sut.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_RESET_TOKEN");
    }

    [Fact]
    public async Task ResetPasswordAsync_WrongToken_ThrowsInvalidResetToken()
    {
        // Arrange
        User user = TestData.CreateUser();
        user.PasswordResetToken = "correct-token";
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        _userRepo.GetByEmailForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "wrong-token", NewPassword = "NewPass123!" };

        // Act
        Func<Task> act = () => _sut.ResetPasswordAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_RESET_TOKEN");
    }
}
