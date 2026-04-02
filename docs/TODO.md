# TODO — Stubs & Deferred Work

> Items that are intentionally stubbed or deferred during initial module implementation.

## Auth Module

### Stubbed

| Item                      | Location                                                     | Description                                                                                             |
| ------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| OAuth Provider Validation | `Auth.Infrastructure/Security/StubOAuthProviderValidator.cs` | Returns deterministic stub data instead of calling Google/Apple/Facebook APIs. Logs warning in non-dev. |

### Deferred

| Item                       | Description                                                                                       |
| -------------------------- | ------------------------------------------------------------------------------------------------- |
| EF Core Migration          | `InitialAuthSchema` migration not yet generated. Run `dotnet ef migrations add` when DB is ready. |
| Email Verification Flow    | `email_verified` flag is set but no verification email is sent. Needs email service integration.  |
| Password Reset Flow        | No forgot-password / reset-password endpoints yet.                                                |
| Account Lockout            | No brute-force protection (failed attempt tracking / lockout).                                    |
| Rate Limiting              | No per-endpoint rate limiting on auth endpoints.                                                  |
| Integration Tests          | Auth module integration tests with Testcontainers not yet written.                                |
| IUnitOfWork Disambiguation | Both Tenancy and Auth register `IUnitOfWork`. Last registration wins. Consider keyed services.    |
