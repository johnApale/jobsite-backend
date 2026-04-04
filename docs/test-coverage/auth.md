# Auth Module Test Coverage

← [Test Coverage](README.md)

> Tests for authentication — user entities, JWT tokens, password hashing, OAuth, refresh token rotation, and input validation.

---

## `UserTests` (4 tests)

Tests the `User` aggregate root entity structure, domain event raising, and default collection initialization.

| Test                                         | What It Verifies                                            | Expected Outcome                         |
| -------------------------------------------- | ----------------------------------------------------------- | ---------------------------------------- |
| `CreateUser_WithValidData_SetsProperties`    | User creation with valid data sets all properties correctly | Properties match provided values         |
| `Raise_DomainEvent_IsCapturedInDomainEvents` | `Raise()` method adds domain events to the collection       | `DomainEvents` contains the raised event |
| `ExternalLogins_DefaultsToEmptyList`         | New user starts with no external logins                     | `ExternalLogins` is empty                |
| `RefreshTokens_DefaultsToEmptyList`          | New user starts with no refresh tokens                      | `RefreshTokens` is empty                 |

**Why:** The `User` aggregate root is the central entity of the Auth module. The `Raise()` method wrapper is essential because `RaiseDomainEvent` is protected on `AggregateRoot` — if this delegation breaks, `UserRegisteredEvent` will never fire.

---

## `RefreshTokenTests` (4 tests)

Tests the `RefreshToken` entity behavior, specifically the revocation and expiration logic.

| Test                                           | What It Verifies                                         | Expected Outcome                       |
| ---------------------------------------------- | -------------------------------------------------------- | -------------------------------------- |
| `Revoke_SetsIsRevokedAndRevokedAt`             | `Revoke()` sets `IsRevoked = true` and records timestamp | Both properties updated correctly      |
| `IsExpired_WhenExpiresAtInPast_ReturnsTrue`    | Expired tokens are detected correctly                    | Returns `true`                         |
| `IsExpired_WhenExpiresAtInFuture_ReturnsFalse` | Non-expired tokens are detected correctly                | Returns `false`                        |
| `CreateRefreshToken_DefaultsIsRevokedToFalse`  | New tokens start as not revoked                          | `IsRevoked` is false, `RevokedAt` null |

**Why:** Refresh token revocation and expiration are security-critical. If `Revoke()` doesn't set both flags, token replay detection breaks. If `IsExpired` calculations are wrong, expired tokens could be accepted.

---

## `PasswordHasherTests` (5 tests)

Tests the BCrypt password hashing implementation.

| Test                                                       | What It Verifies                                       | Expected Outcome             |
| ---------------------------------------------------------- | ------------------------------------------------------ | ---------------------------- |
| `HashPassword_ReturnsNonEmptyHash`                         | BCrypt produces a valid hash starting with `$2a$`      | Non-empty BCrypt hash string |
| `HashPassword_DifferentInputs_ProduceDifferentHashes`      | Different passwords produce different hashes           | Hashes are different         |
| `HashPassword_SameInput_ProducesDifferentHashes_DueToSalt` | Same password produces unique hashes (salt uniqueness) | Hashes are different         |
| `VerifyPassword_CorrectPassword_ReturnsTrue`               | Correct password verifies against its hash             | Returns `true`               |
| `VerifyPassword_WrongPassword_ReturnsFalse`                | Wrong password fails verification                      | Returns `false`              |

**Why:** Password hashing is the most security-critical code in the Auth module. Failures here could allow password bypass (if verify always returns true) or lock out all users (if hashing is broken).

---

## `JwtServiceTests` (9 tests)

Tests JWT access token generation, refresh token generation, and SHA-256 token hashing.

| Test                                                  | What It Verifies                                            | Expected Outcome               |
| ----------------------------------------------------- | ----------------------------------------------------------- | ------------------------------ |
| `GenerateAccessToken_ReturnsValidJwt`                 | Generated token is a valid JWT with correct issuer/audience | Valid JWT structure            |
| `GenerateAccessToken_ContainsExpectedClaims`          | Token contains sub, email, role, tenant_id claims           | All claims present with values |
| `GenerateAccessToken_ExpiresInConfiguredMinutes`      | Token expiration matches configured value                   | Expires ~30 minutes from now   |
| `GenerateRefreshToken_ReturnsNonEmptyBase64String`    | Refresh token is 64-byte base64-encoded string              | Valid base64, 64 bytes         |
| `GenerateRefreshToken_ProducesUniqueTokens`           | Each generated token is unique                              | Two tokens are different       |
| `HashToken_SameInput_ProducesSameHash`                | SHA-256 hash is deterministic                               | Same hash for same input       |
| `HashToken_DifferentInputs_ProduceDifferentHashes`    | Different inputs produce different hashes                   | Different hashes               |
| `AccessTokenExpirationMinutes_ReturnsConfiguredValue` | Configuration value is exposed correctly                    | Returns 30                     |
| `RefreshTokenExpirationDays_ReturnsConfiguredValue`   | Configuration value is exposed correctly                    | Returns 7                      |

**Why:** JWT tokens are the primary authentication mechanism. Incorrect claims would break authorization policies. Non-deterministic token hashing would break refresh token lookup. Predictable refresh tokens would be a security vulnerability.

---

## `AuthServiceTests` (18 tests)

Tests the `AuthService` application service with mocked dependencies. Covers register, login, refresh, OAuth, logout, and get-current-user flows.

| Test                                                         | What It Verifies                                         | Expected Outcome                           |
| ------------------------------------------------------------ | -------------------------------------------------------- | ------------------------------------------ |
| `RegisterAsync_ValidRequest_ReturnsTokens`                   | Successful registration issues access and refresh tokens | Tokens returned, user added                |
| `RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailError`     | Duplicate email throws `DUPLICATE_EMAIL`                 | `AppError` with code `DUPLICATE_EMAIL`     |
| `RegisterAsync_NoRoleProvided_DefaultsToApplicant`           | Missing role defaults to `Applicant`                     | User created with `Applicant` role         |
| `RegisterAsync_RaisesUserRegisteredEvent`                    | Registration raises `UserRegisteredEvent`                | User has 1 domain event                    |
| `LoginAsync_ValidCredentials_ReturnsTokens`                  | Valid email/password returns tokens                      | Tokens returned                            |
| `LoginAsync_UserNotFound_ThrowsInvalidCredentials`           | Non-existent email throws `INVALID_CREDENTIALS`          | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_DeactivatedUser_ThrowsInvalidCredentials`        | Deactivated user cannot login                            | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_NullPasswordHash_ThrowsInvalidCredentials`       | OAuth-only user cannot login with password               | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_WrongPassword_ThrowsInvalidCredentials`          | Wrong password throws error                              | `AppError` with code `INVALID_CREDENTIALS` |
| `LoginAsync_ValidCredentials_UpdatesLastLoginAt`             | Successful login updates `LastLoginAt`                   | Timestamp set to now                       |
| `RefreshTokenAsync_ValidToken_ReturnsNewTokens`              | Valid refresh rotates and returns new tokens             | New tokens, old token revoked              |
| `RefreshTokenAsync_TokenNotFound_ThrowsInvalidCredentials`   | Unknown token throws error                               | `AppError` with code `INVALID_CREDENTIALS` |
| `RefreshTokenAsync_RevokedToken_RevokesEntireFamily`         | Reused revoked token triggers family-wide revocation     | `TOKEN_REPLAY_DETECTED`, family revoked    |
| `RefreshTokenAsync_ExpiredToken_ThrowsTokenExpired`          | Expired token throws `TOKEN_EXPIRED`                     | `AppError` with code `TOKEN_EXPIRED`       |
| `RefreshTokenAsync_DeactivatedUser_ThrowsInvalidCredentials` | Deactivated user cannot refresh                          | `AppError` with code `INVALID_CREDENTIALS` |
| `LogoutAsync_ValidToken_RevokesIt`                           | Logout revokes the refresh token                         | Token revoked, changes saved               |
| `LogoutAsync_TokenNotFound_DoesNotThrow`                     | Logout with invalid token is idempotent                  | No exception thrown                        |
| `LogoutAsync_AlreadyRevokedToken_DoesNotSaveAgain`           | Already-revoked token doesn't trigger save               | `SaveChangesAsync` not called              |
| `GetCurrentUserAsync_ExistingUser_ReturnsUserResponse`       | Returns user profile for valid user ID                   | UserResponse with correct data             |
| `GetCurrentUserAsync_UserNotFound_ThrowsUserNotFound`        | Non-existent user throws `USER_NOT_FOUND`                | `AppError` with code `USER_NOT_FOUND`      |
| `OAuthLoginAsync_InvalidProvider_ThrowsInvalidRequest`       | Invalid provider name throws error                       | `AppError` with code `INVALID_REQUEST`     |
| `OAuthLoginAsync_ExistingLinkedAccount_ReturnsTokens`        | User with linked OAuth account gets tokens               | Tokens returned, `LastLoginAt` updated     |
| `OAuthLoginAsync_NewUser_CreatesUserAndReturnsTokens`        | New OAuth user is created with linked provider           | User created with external login           |

**Why:** `AuthService` contains all authentication business logic. These tests verify every authentication path including edge cases like deactivated users, OAuth-only users, token replay detection, and idempotent logout.

---

## `RegisterRequestValidatorTests` (7 tests)

Tests FluentValidation rules for the registration request.

| Test                                        | What It Verifies                                    | Expected Outcome    |
| ------------------------------------------- | --------------------------------------------------- | ------------------- |
| `Validate_ValidRequest_IsValid`             | Valid request passes all rules                      | `IsValid` is true   |
| `Validate_EmptyEmail_HasValidationError`    | Empty email is rejected                             | Error on `Email`    |
| `Validate_InvalidEmail_HasValidationError`  | Malformed email is rejected                         | Error on `Email`    |
| `Validate_ShortPassword_HasValidationError` | Password under 8 chars is rejected                  | Error on `Password` |
| `Validate_ValidRole_IsValid`                | Valid role passes validation                        | `IsValid` is true   |
| `Validate_InvalidRole_HasValidationError`   | Invalid role string is rejected                     | Error on `Role`     |
| `Validate_NullRole_IsValid`                 | Null role is accepted (defaults to Applicant later) | `IsValid` is true   |

**Why:** Input validation is the first line of defense. Malformed requests should be caught before reaching the service layer.

---

## `AuthConstantsTests` (6 tests)

Tests domain constant validation methods for `UserRole`, `UserStatus`, and `ExternalLoginProvider`.

| Test                                                         | What It Verifies                         | Expected Outcome |
| ------------------------------------------------------------ | ---------------------------------------- | ---------------- |
| `UserRole_IsValid_ValidRole_ReturnsTrue`                     | All 5 valid roles pass validation        | Returns `true`   |
| `UserRole_IsValid_InvalidRole_ReturnsFalse`                  | Invalid/lowercase roles are rejected     | Returns `false`  |
| `UserStatus_IsValid_ValidStatus_ReturnsTrue`                 | All 3 valid statuses pass validation     | Returns `true`   |
| `UserStatus_IsValid_InvalidStatus_ReturnsFalse`              | Invalid statuses are rejected            | Returns `false`  |
| `ExternalLoginProvider_IsValid_ValidProvider_ReturnsTrue`    | All 3 valid providers pass validation    | Returns `true`   |
| `ExternalLoginProvider_IsValid_InvalidProvider_ReturnsFalse` | Invalid/lowercase providers are rejected | Returns `false`  |

**Why:** These constants must match PostgreSQL CHECK constraints exactly. Case mismatches would cause runtime database errors.
