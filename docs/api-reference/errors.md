# Error Reference

All API errors are returned in a canonical envelope format, serialized with `snake_case` property names.

## Error Envelope

```json
{
  "code": "SCREAMING_SNAKE_CASE",
  "message": "Human-readable description",
  "details": {
    "field_name": "Validation error for this field"
  },
  "request_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

| Field        | Type     | Always Present | Description                                                                   |
| ------------ | -------- | -------------- | ----------------------------------------------------------------------------- |
| `code`       | `string` | Yes            | Machine-readable error code in `SCREAMING_SNAKE_CASE`                         |
| `message`    | `string` | Yes            | Human-readable error description                                              |
| `details`    | `object` | No             | Per-field validation errors. Omitted when not applicable.                     |
| `request_id` | `string` | Yes            | Correlation ID for tracing (from `X-Correlation-ID` header or auto-generated) |

## Error Codes

### 400 Bad Request

| Code                    | Default Message                               | Description                                                          |
| ----------------------- | --------------------------------------------- | -------------------------------------------------------------------- |
| `VALIDATION_ERROR`      | Request validation failed                     | FluentValidation rule failures. `details` contains per-field errors. |
| `INVALID_REQUEST`       | Structurally invalid request                  | Malformed JSON, missing required fields, or type mismatches.         |
| `DUPLICATE_EMAIL`       | Email already registered for this tenant      | Attempt to register with an email that already exists in the tenant. |
| `DUPLICATE_APPLICATION` | Applicant already applied to this job posting | One-application-per-person-per-job constraint violated.              |

### 401 Unauthorized

| Code                    | Default Message                   | Description                                                                         |
| ----------------------- | --------------------------------- | ----------------------------------------------------------------------------------- |
| `UNAUTHORIZED`          | Missing or invalid authentication | No token provided or token is invalid.                                              |
| `INVALID_CREDENTIALS`   | Wrong email or password           | Login attempt with incorrect credentials.                                           |
| `TOKEN_EXPIRED`         | Token has expired                 | Access or refresh token has passed its expiration.                                  |
| `TOKEN_REPLAY_DETECTED` | Refresh token reuse detected      | A previously used refresh token was presented. All tokens for the user are revoked. |

### 403 Forbidden

| Code        | Default Message                                    | Description                                                                                                        |
| ----------- | -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `FORBIDDEN` | You do not have permission to access this resource | Authenticated user lacks the required role or permission. Also returned when the tenant is not in `Active` status. |

### 404 Not Found

| Code                    | Default Message       | Description                                      |
| ----------------------- | --------------------- | ------------------------------------------------ |
| `TENANT_NOT_FOUND`      | Tenant not found      | No tenant exists with the given ID or subdomain. |
| `USER_NOT_FOUND`        | User not found        | No user exists with the given ID.                |
| `JOB_POSTING_NOT_FOUND` | Job posting not found | No job posting exists with the given ID.         |
| `APPLICATION_NOT_FOUND` | Application not found | No application exists with the given ID.         |
| `PROFILE_NOT_FOUND`     | Profile not found     | No applicant profile exists for the given user.  |

### 409 Conflict

| Code                            | Default Message                        | Description                                  |
| ------------------------------- | -------------------------------------- | -------------------------------------------- |
| `APPLICATION_ALREADY_WITHDRAWN` | Application has already been withdrawn | Attempt to act on a withdrawn application.   |
| `OFFER_ALREADY_ACCEPTED`        | Offer has already been accepted        | Attempt to modify an already-accepted offer. |

### 422 Unprocessable Entity

| Code                   | Default Message                        | Description                                                      |
| ---------------------- | -------------------------------------- | ---------------------------------------------------------------- |
| `UNPROCESSABLE_ENTITY` | Business logic prevents this operation | The request is syntactically valid but violates a business rule. |

### 429 Too Many Requests

| Code           | Default Message     | Description                                   |
| -------------- | ------------------- | --------------------------------------------- |
| `RATE_LIMITED` | Rate limit exceeded | Client has exceeded the allowed request rate. |

### 500 Internal Server Error

| Code             | Default Message              | Description                                                          |
| ---------------- | ---------------------------- | -------------------------------------------------------------------- |
| `INTERNAL_ERROR` | An unexpected error occurred | Unhandled server error. The `request_id` can be used to locate logs. |

### 503 Service Unavailable

| Code                  | Default Message                             | Description                                                                     |
| --------------------- | ------------------------------------------- | ------------------------------------------------------------------------------- |
| `SERVICE_UNAVAILABLE` | A required service is currently unavailable | A downstream dependency (database, message broker, AI provider) is unreachable. |

## Examples

### Validation Error (400)

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": {
    "subdomain": "Subdomain must be between 3 and 63 characters",
    "owner_email": "Must be a valid email address"
  },
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Unauthorized (401)

```json
{
  "code": "TOKEN_EXPIRED",
  "message": "Token has expired",
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Not Found (404)

```json
{
  "code": "TENANT_NOT_FOUND",
  "message": "Tenant not found",
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Internal Server Error (500)

```json
{
  "code": "INTERNAL_ERROR",
  "message": "An unexpected error occurred",
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

## Custom Error Messages

Error sentinels can be thrown with a custom message using `.WithMessage()`:

```csharp
throw AppErrors.UserNotFound.WithMessage($"User {userId} not found in tenant");
```

The `code` and `status_code` remain the same; only the `message` field changes.

## Validation Details

For `VALIDATION_ERROR` responses, attach per-field details using `.WithDetails()`:

```csharp
throw AppErrors.Validation.WithDetails(new Dictionary<string, string>
{
    ["email"] = "Must be a valid email address",
    ["name"] = "Name is required"
});
```
