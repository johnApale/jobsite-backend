# API Conventions

> HTTP endpoint design, headers, authentication, and request/response conventions for D'Jobsite iConnect.

## Route Structure

The monolith organizes routes into three tiers:

| Tier      | Path Prefix          | Authentication | Purpose                                   |
| --------- | -------------------- | -------------- | ----------------------------------------- |
| Health    | `/health`, `/ready`  | None           | Infrastructure probes                     |
| Public    | `/api/v1/{module}/*` | None           | Login, registration, token refresh        |
| Protected | `/api/v1/{module}/*` | JWT Bearer     | All authenticated client-facing endpoints |

### Module Route Prefixes

| Module       | Route Prefix          | DB Schema        |
| ------------ | --------------------- | ---------------- |
| Tenancy      | `/api/v1/tenants`     | `catalog.*`      |
| Auth         | `/api/v1/auth`        | `auth.*`         |
| Admin        | `/api/v1/admin`       | `admin.*`        |
| Profiles     | `/api/v1/profiles`    | `profiles.*`     |
| Recruitment  | `/api/v1/recruitment` | `recruitment.*`  |
| Screening    | `/api/v1/screening`   | `screening.*`    |
| Matching     | `/api/v1/matching`    | `matching.*`     |
| HR Workflows | `/api/v1/hr`          | `hr_workflows.*` |

> **Note:** The AI Interview Service is a separate FastAPI deployment with its own route structure (`/api/v1/interviews/*`). It does not share the monolith's gateway.

## Authentication Model

### JWT Bearer (All Protected Endpoints)

The monolith issues JWTs via the Auth module. Every protected request includes a `Bearer` token in the `Authorization` header. The JWT contains tenant, user, and role claims. `TenantResolutionMiddleware` resolves the tenant from the subdomain and sets up the per-tenant `DbContext` before the JWT is validated.

JWT claims payload:

```json
{
  "sub": "uuid",
  "tenant_id": "uuid",
  "role": "string",
  "email": "string"
}
```

The JWT secret is shared between the monolith and the AI Interview Service so both can validate tokens independently.

### Public Endpoints

Some endpoints require no authentication (login, registration, token refresh, health checks). These are explicitly marked with `.AllowAnonymous()`.

### AI Interview Service Authentication

The AI Interview Service validates the same JWTs. It receives tenant context from the JWT claims — not from subdomain resolution.

## Standard Headers

### Request Headers

| Header             | Required          | Source    | Description                                    |
| ------------------ | ----------------- | --------- | ---------------------------------------------- |
| `Authorization`    | Protected routes  | Client    | `Bearer {jwt}` token                           |
| `X-Correlation-ID` | All               | Generated | Request correlation ID for distributed tracing |
| `Content-Type`     | When body present | Client    | Must be `application/json` for JSON bodies     |

### Response Headers

| Header             | Required | Description                              |
| ------------------ | -------- | ---------------------------------------- |
| `Content-Type`     | Always   | `application/json` for all API responses |
| `X-Correlation-ID` | Always   | Echo the request correlation ID          |

### Correlation ID Propagation

Every component must:

1. Read `X-Correlation-ID` from inbound requests.
2. If absent, generate a UUID.
3. Include it in all published message broker events.
4. Include it as `request_id` in error responses.
5. Attach it to structured log entries.

Implementation:

- **Monolith:** `CorrelationIdMiddleware` reads/generates the ID and stores it in `HttpContext.Items["CorrelationId"]`.
- **AI Interview Service:** FastAPI middleware reads/generates the ID and stores it in request state.

## Request/Response Format

### JSON Casing

All request and response bodies use **`snake_case`** field names. Configured globally — not per-endpoint.

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
```

### Success Responses

| Status           | When                                  | Body                                 |
| ---------------- | ------------------------------------- | ------------------------------------ |
| `200 OK`         | Successful retrieval or update        | Resource or result object            |
| `201 Created`    | Successful creation                   | Created resource + `Location` header |
| `204 No Content` | Successful deletion or void operation | Empty                                |

### Error Responses

All errors use the [error envelope specification](ERROR_ENVELOPE.md).

### Pagination

List endpoints that may return large result sets support cursor or offset pagination:

```
GET /api/v1/recruitment/job-postings?page=1&page_size=25
```

Response includes pagination metadata:

```json
{
  "data": [...],
  "pagination": {
    "page": 1,
    "page_size": 25,
    "total_count": 142,
    "total_pages": 6
  }
}
```

### Null Handling

- Omit null fields from responses when possible (configured globally via `JsonIgnoreCondition.WhenWritingNull`).
- Exception: fields that are semantically meaningful when null (e.g., `completed_at: null` to indicate an in-progress item) should be explicitly included with `[JsonIgnore(Condition = JsonIgnoreCondition.Never)]`.

## Endpoint Metadata (Scalar)

The monolith exposes Scalar API documentation in development. Every endpoint must chain metadata for the OpenAPI spec:

```csharp
routes.MapPost("/", handler)
    .WithTags("JobPostings")
    .WithName("CreateJobPosting")
    .WithSummary("Create a new job posting")
    .WithDescription("Detailed description in Markdown...")
    .Produces<JobPostingResponse>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status401Unauthorized);
```

Required metadata on every endpoint:

| Method               | Purpose                    |
| -------------------- | -------------------------- |
| `.WithTags()`        | Sidebar grouping           |
| `.WithName()`        | Unique operation ID        |
| `.WithSummary()`     | One-line title             |
| `.WithDescription()` | Markdown description       |
| `.Produces<T>()`     | Each success response type |
| `.Produces()`        | Each error status code     |

## Validation

Use **FluentValidation** with `IValidator<T>`. Validate inline in the endpoint handler:

```csharp
ValidationResult validation = await validator.ValidateAsync(request, ct);
if (!validation.IsValid)
    throw AppErrors.Validation.WithDetails(validation.ToDictionary());
```

Or via endpoint filter:

```csharp
.AddEndpointFilter<ValidationFilter<CreateJobPostingRequest>>()
```

## Lookups API

Enum values exposed to frontends are served from a lookups endpoint. This keeps the frontend in sync with the CHECK constraints without hardcoding values.

```
GET /api/v1/lookups/{type}
```

Returns valid values for a given enum type (e.g., `application-statuses`, `employment-types`). Reads from the same C# enum/constant definitions that back the CHECK constraints.

## Inter-Module Communication

Modules within the monolith **never** call each other's endpoints directly. Communication patterns:

| Pattern                         | Mechanism                                     | When                                                              |
| ------------------------------- | --------------------------------------------- | ----------------------------------------------------------------- |
| Module → Module (same process)  | In-process domain events                      | Application submitted, screening completed, candidate shortlisted |
| Monolith → AI Interview Service | Message broker (RabbitMQ / Azure Service Bus) | `CandidateReadyForInterviewEvent`                                 |
| AI Interview Service → Monolith | Message broker                                | `InterviewCompletedEvent`                                         |

There are no internal HTTP calls between modules. There are no `X-Internal-Token` headers. The modular monolith shares a process — events are the API between modules.
