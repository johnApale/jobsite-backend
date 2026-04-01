# Error Envelope Specification

> The canonical error response shape for all D'Jobsite iConnect services. One format. Every service. No exceptions.

## The Standard Format

Every error response from both the monolith and the AI Interview Service must use this flat JSON structure:

```json
{
  "code": "APPLICATION_NOT_FOUND",
  "message": "Application not found",
  "request_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

With optional `details` for validation errors:

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": {
    "email": "Email is required",
    "job_posting_id": "Must be a valid UUID"
  },
  "request_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Field Definitions

| Field        | Type             | Required | Description                                                                                                                       |
| ------------ | ---------------- | -------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `code`       | `string`         | Yes      | Machine-readable error code in `SCREAMING_SNAKE_CASE`                                                                             |
| `message`    | `string`         | Yes      | Human-readable description. May include dynamic context (e.g., "Application {id} not found")                                      |
| `details`    | `object \| null` | No       | Structured details for validation errors (field → message map). Omit entirely when not applicable — do not send `"details": null` |
| `request_id` | `string`         | Yes      | Correlation/request ID for tracing. Sourced from `X-Correlation-ID` header, falling back to a generated UUID                      |

## Response Content Type

All error responses must set:

```
Content-Type: application/json
```

## HTTP Status Code Mapping

### 400 Bad Request

| Code                    | When                                                                                        |
| ----------------------- | ------------------------------------------------------------------------------------------- |
| `VALIDATION_ERROR`      | Request body or query parameter fails validation. `details` should contain per-field errors |
| `INVALID_REQUEST`       | Structurally invalid request (malformed JSON, wrong content type)                           |
| `DUPLICATE_EMAIL`       | Email already registered for this tenant                                                    |
| `DUPLICATE_APPLICATION` | Applicant already applied to this job posting                                               |

### 401 Unauthorized

| Code                    | When                                                        |
| ----------------------- | ----------------------------------------------------------- |
| `UNAUTHORIZED`          | Missing or invalid authentication (no token, malformed JWT) |
| `INVALID_CREDENTIALS`   | Wrong email or password on login                            |
| `TOKEN_EXPIRED`         | JWT or refresh token has expired                            |
| `TOKEN_REPLAY_DETECTED` | Refresh token reuse detected (potential token theft)        |

### 403 Forbidden

| Code        | When                                                                        |
| ----------- | --------------------------------------------------------------------------- |
| `FORBIDDEN` | Authenticated but lacks permission (e.g., Applicant accessing admin routes) |

### 404 Not Found

| Code                 | When                                                                                                                 |
| -------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `{ENTITY}_NOT_FOUND` | Entity does not exist (e.g., `USER_NOT_FOUND`, `JOB_POSTING_NOT_FOUND`, `APPLICATION_NOT_FOUND`, `TENANT_NOT_FOUND`) |

### 409 Conflict

| Code                | When                                                                             |
| ------------------- | -------------------------------------------------------------------------------- |
| `{ENTITY}_CONFLICT` | State conflict (e.g., `APPLICATION_ALREADY_WITHDRAWN`, `OFFER_ALREADY_ACCEPTED`) |

### 422 Unprocessable Entity

| Code                   | When                                                                                                           |
| ---------------------- | -------------------------------------------------------------------------------------------------------------- |
| `UNPROCESSABLE_ENTITY` | Business logic prevents the operation (valid request, but can't be fulfilled — e.g., applying to a closed job) |

### 429 Too Many Requests

| Code           | When                |
| -------------- | ------------------- |
| `RATE_LIMITED` | Rate limit exceeded |

### 500 Internal Server Error

| Code             | When                                                                                                       |
| ---------------- | ---------------------------------------------------------------------------------------------------------- |
| `INTERNAL_ERROR` | Unhandled exception or unexpected failure. **Never leak stack traces or internal details in the message.** |

### 503 Service Unavailable

| Code                  | When                                                       |
| --------------------- | ---------------------------------------------------------- |
| `SERVICE_UNAVAILABLE` | Dependency is down (database, message broker, AI provider) |

## Implementation

### Monolith (.NET — AppErrorMiddleware)

```csharp
// SharedKernel/Errors/AppErrorMiddleware.cs

public sealed class AppErrorMiddleware
{
    private readonly RequestDelegate _next;

    public AppErrorMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppError ex)
        {
            string requestId = context.Items["CorrelationId"]?.ToString()
                            ?? context.TraceIdentifier;

            object body = new
            {
                code = ex.Code,
                message = ex.Message,
                details = ex.Details,       // omitted by serializer when null
                request_id = requestId
            };

            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(body);
        }
        catch (Exception)
        {
            string requestId = context.Items["CorrelationId"]?.ToString()
                            ?? context.TraceIdentifier;

            object body = new
            {
                code = "INTERNAL_ERROR",
                message = "An unexpected error occurred",
                request_id = requestId
            };

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(body);
        }
    }
}
```

### AI Interview Service (Python — FastAPI)

```python
# app/core/errors.py

from fastapi import Request
from fastapi.responses import JSONResponse

class AppError(Exception):
    def __init__(self, code: str, status_code: int, message: str, details: dict | None = None):
        self.code = code
        self.status_code = status_code
        self.message = message
        self.details = details

async def app_error_handler(request: Request, exc: AppError) -> JSONResponse:
    body = {
        "code": exc.code,
        "message": exc.message,
        "request_id": request.state.correlation_id,
    }
    if exc.details:
        body["details"] = exc.details
    return JSONResponse(status_code=exc.status_code, content=body)
```

## Error Code Naming Rules

1. Use `SCREAMING_SNAKE_CASE`.
2. Entity-specific codes use the entity name as prefix: `APPLICATION_NOT_FOUND`, `USER_NOT_FOUND`, `JOB_POSTING_NOT_FOUND`.
3. Generic codes use the category: `VALIDATION_ERROR`, `UNAUTHORIZED`, `INTERNAL_ERROR`.
4. Keep codes stable — once a code is in use, don't rename it without coordinating all consumers.
5. Both the monolith and the AI Interview Service must use the same error envelope format.
