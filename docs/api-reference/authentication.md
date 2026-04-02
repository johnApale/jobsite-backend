# Authentication

## JWT Bearer

The API uses **JWT Bearer** authentication with **HS256** (HMAC-SHA256) symmetric signing.

### Token Format

Tokens contain the following claims:

| Claim       | Description                                               |
| ----------- | --------------------------------------------------------- |
| `sub`       | User ID (UUID)                                            |
| `iss`       | Issuer (`djobsite-iconnect`)                              |
| `aud`       | Audience (`djobsite-iconnect`)                            |
| `exp`       | Expiration timestamp                                      |
| `tenant_id` | Tenant UUID                                               |
| `role`      | User role (e.g., `AgencyAdmin`, `Recruiter`, `Applicant`) |

### Token Lifetimes

| Token Type    | Default Lifetime |
| ------------- | ---------------- |
| Access token  | 60 minutes       |
| Refresh token | 30 days          |

### Usage

Include the access token in the `Authorization` header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
```

### Token Validation

The server validates:

- **Issuer** — must match configured `JwtIssuer`
- **Audience** — must match configured `JwtAudience`
- **Lifetime** — token must not be expired (zero clock skew)
- **Signing key** — HMAC-SHA256 signature verification

### Error Responses

| Scenario                 | Error Code              | Status |
| ------------------------ | ----------------------- | ------ |
| No token provided        | `UNAUTHORIZED`          | 401    |
| Invalid/malformed token  | `UNAUTHORIZED`          | 401    |
| Expired access token     | `TOKEN_EXPIRED`         | 401    |
| Expired refresh token    | `TOKEN_EXPIRED`         | 401    |
| Refresh token reuse      | `TOKEN_REPLAY_DETECTED` | 401    |
| Insufficient permissions | `FORBIDDEN`             | 403    |

## Tenant Resolution

In addition to JWT authentication, most endpoints require a **tenant context** resolved from the request's `Host` header subdomain.

### How It Works

1. The `TenantResolutionMiddleware` extracts the subdomain from the `Host` header
2. Example: `acme.djobsite.com` → subdomain `acme`
3. The subdomain is used to look up the tenant in the Catalog database
4. The tenant must be in `Active` status
5. The tenant entity and its connection string are stored in `HttpContext.Items` for downstream use

### Subdomain Rules

- Must have at least three hostname parts (e.g., `subdomain.domain.tld`)
- `localhost` and IP addresses have no tenant context (returns 400)
- Subdomain is normalized to lowercase

### Bypass Routes

The following routes skip tenant resolution entirely:

| Route               | Reason                                                  |
| ------------------- | ------------------------------------------------------- |
| `/health`           | Infrastructure health probe                             |
| `/ready`            | Infrastructure readiness probe                          |
| `/api/v1/tenants/*` | Tenant registration and lookup (operates on Catalog DB) |
| `/scalar`           | API documentation UI                                    |
| `/openapi`          | OpenAPI specification                                   |

### Tenant Resolution Errors

| Scenario                                                   | Status | Error Code         |
| ---------------------------------------------------------- | ------ | ------------------ |
| Cannot extract subdomain (localhost, IP, single-part host) | 400    | `INVALID_REQUEST`  |
| Subdomain does not match any tenant                        | 404    | `TENANT_NOT_FOUND` |
| Tenant exists but status is not `Active`                   | 403    | `FORBIDDEN`        |

## Required Headers Summary

| Header             | Required            | Description                                               |
| ------------------ | ------------------- | --------------------------------------------------------- |
| `Authorization`    | Protected endpoints | `Bearer <access_token>`                                   |
| `Host`             | Protected endpoints | Must include tenant subdomain (e.g., `acme.djobsite.com`) |
| `X-Correlation-ID` | No                  | UUID for distributed tracing. Auto-generated if absent.   |
| `Content-Type`     | Request body        | `application/json`                                        |
