# Auth Module API

## Base Path

```
/api/v1/auth
```

All auth endpoints require **tenant resolution** (subdomain in `Host` header) but do not require JWT authentication unless noted.

## Endpoints

### Register

Create a new user account with email/password credentials.

```
POST /api/v1/auth/register
```

**Request Body:**

| Field        | Type     | Required | Description                                   |
| ------------ | -------- | -------- | --------------------------------------------- |
| `email`      | `string` | Yes      | Login email (max 254 chars)                   |
| `password`   | `string` | Yes      | Password (min 8 chars)                        |
| `first_name` | `string` | Yes      | First name (max 100 chars)                    |
| `last_name`  | `string` | Yes      | Last name (max 100 chars)                     |
| `role`       | `string` | No       | Defaults to `Applicant`. See [Roles](#roles). |

**Response:** `201 Created`

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIs...",
  "refresh_token": "dGhpcyBpcyBhIHJlZnJl...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

**Errors:**

| Code               | Status | Condition            |
| ------------------ | ------ | -------------------- |
| `DUPLICATE_EMAIL`  | 400    | Email already in use |
| `VALIDATION_ERROR` | 400    | Invalid request body |

---

### Login

Authenticate with email/password and receive tokens.

```
POST /api/v1/auth/login
```

**Request Body:**

| Field      | Type     | Required | Description |
| ---------- | -------- | -------- | ----------- |
| `email`    | `string` | Yes      | Login email |
| `password` | `string` | Yes      | Password    |

**Response:** `200 OK`

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIs...",
  "refresh_token": "dGhpcyBpcyBhIHJlZnJl...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

**Errors:**

| Code                  | Status | Condition                                |
| --------------------- | ------ | ---------------------------------------- |
| `INVALID_CREDENTIALS` | 401    | Wrong email/password or user deactivated |
| `VALIDATION_ERROR`    | 400    | Invalid request body                     |

---

### Refresh Token

Exchange a valid refresh token for new access and refresh tokens. Implements **rotation with family-based replay detection**.

```
POST /api/v1/auth/refresh
```

**Request Body:**

| Field           | Type     | Required | Description           |
| --------------- | -------- | -------- | --------------------- |
| `refresh_token` | `string` | Yes      | Current refresh token |

**Response:** `200 OK`

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIs...",
  "refresh_token": "bmV3IHJlZnJlc2ggdG9r...",
  "expires_in": 3600,
  "token_type": "Bearer"
}
```

**Errors:**

| Code                    | Status | Condition                             |
| ----------------------- | ------ | ------------------------------------- |
| `INVALID_CREDENTIALS`   | 401    | Token not found or user deactivated   |
| `TOKEN_EXPIRED`         | 401    | Refresh token has expired             |
| `TOKEN_REPLAY_DETECTED` | 401    | Reused revoked token — family revoked |

---

### OAuth Login

Authenticate via an external OAuth provider. Links the provider account if the user already exists, or creates a new account.

```
POST /api/v1/auth/oauth/{provider}
```

**Path Parameters:**

| Parameter  | Type     | Description                                   |
| ---------- | -------- | --------------------------------------------- |
| `provider` | `string` | OAuth provider: `Google`, `Apple`, `Facebook` |

**Request Body:**

| Field            | Type     | Required | Description                   |
| ---------------- | -------- | -------- | ----------------------------- |
| `provider_token` | `string` | Yes      | ID/access token from provider |
| `email`          | `string` | Yes      | Email from OAuth provider     |
| `display_name`   | `string` | No       | Display name from provider    |

**Response:** `200 OK` — Same token response as login.

**Errors:**

| Code                  | Status | Condition            |
| --------------------- | ------ | -------------------- |
| `INVALID_REQUEST`     | 400    | Unsupported provider |
| `INVALID_CREDENTIALS` | 401    | User deactivated     |

> **Note:** OAuth provider token validation is currently **stubbed**. See [TODO](../../docs/TODO.md).

---

### Logout

Revoke a refresh token. Idempotent — succeeds even if the token is already revoked or invalid.

```
POST /api/v1/auth/logout
```

**Requires:** `Authorization: Bearer <access_token>`

**Request Body:**

| Field           | Type     | Required | Description             |
| --------------- | -------- | -------- | ----------------------- |
| `refresh_token` | `string` | Yes      | Refresh token to revoke |

**Response:** `204 No Content`

---

### Get Current User

Return the authenticated user's profile.

```
GET /api/v1/auth/me
```

**Requires:** `Authorization: Bearer <access_token>`

**Response:** `200 OK`

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "first_name": "Jane",
  "last_name": "Doe",
  "role": "Recruiter",
  "status": "Active",
  "email_verified": false,
  "avatar_url": null,
  "created_at": "2026-04-01T12:00:00Z"
}
```

**Errors:**

| Code             | Status | Condition      |
| ---------------- | ------ | -------------- |
| `USER_NOT_FOUND` | 404    | User not found |
| `UNAUTHORIZED`   | 401    | No valid token |

## Roles

| Role            | Description                                |
| --------------- | ------------------------------------------ |
| `Applicant`     | Job seeker (default for self-registration) |
| `Recruiter`     | Manages job postings and applications      |
| `HiringManager` | Reviews candidates and makes decisions     |
| `Interviewer`   | Conducts interviews                        |
| `AgencyAdmin`   | Full administrative access                 |

## Authorization Policies

| Policy Name            | Required Role   |
| ---------------------- | --------------- |
| `RequireApplicant`     | `Applicant`     |
| `RequireRecruiter`     | `Recruiter`     |
| `RequireHiringManager` | `HiringManager` |
| `RequireInterviewer`   | `Interviewer`   |
| `RequireAgencyAdmin`   | `AgencyAdmin`   |
