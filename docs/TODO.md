# TODO — Stubs & Deferred Work

> Items that are intentionally stubbed or deferred during initial module implementation.

## Auth Module

### Stubbed

| Item                      | Location                                                     | Description                                                                                             |
| ------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| OAuth Provider Validation | `Auth.Infrastructure/Security/StubOAuthProviderValidator.cs` | Returns deterministic stub data instead of calling Google/Apple/Facebook APIs. Logs warning in non-dev. |

### Deferred

| Item                    | Description                                                                                      |
| ----------------------- | ------------------------------------------------------------------------------------------------ |
| Email Verification Flow | `email_verified` flag is set but no verification email is sent. Needs email service integration. |
| Password Reset Flow     | No forgot-password / reset-password endpoints yet.                                               |
| Account Lockout         | No brute-force protection (failed attempt tracking / lockout).                                   |
| Rate Limiting           | No per-endpoint rate limiting on auth endpoints.                                                 |

### Completed

| Item                       | Resolution                                                                                                                     |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| EF Core Migration          | `InitialAuthSchema` migration generated and applied. Tables: `auth.users`, `auth.refresh_tokens`, `auth.user_external_logins`. |
| Integration Tests          | 29 Auth integration tests added (UserRepository, RefreshTokenRepository, AuthDbContext).                                       |
| IUnitOfWork Disambiguation | Resolved via keyed services: `AddKeyedScoped<IUnitOfWork>("auth")` / `("catalog")` with `[FromKeyedServices]`.                 |
