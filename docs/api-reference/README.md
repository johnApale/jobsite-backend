# D'Jobsite iConnect — API Reference

Interactive API documentation is available via **Scalar** at [`/scalar`](http://localhost:5166/scalar) when running in Development mode.

## Base URL

```
https://{subdomain}.djobsite.com/api/v1
```

Local development: `http://localhost:5166`

## Authentication

All protected endpoints require a **JWT Bearer** token in the `Authorization` header.

```
Authorization: Bearer <access_token>
```

Tokens are issued as HS256 JWTs with tenant, user, and role claims. See [Authentication](authentication.md) for full details.

## Tenant Resolution

Requests to protected endpoints must include a tenant context via the **subdomain** in the `Host` header (e.g., `acme.djobsite.com`). The middleware resolves the tenant and connects to the tenant-specific database.

Routes that bypass tenant resolution: `/health`, `/ready`, `/api/v1/tenants/*`, `/scalar`, `/openapi`.

## Headers

| Header             | Required            | Description                                                                 |
| ------------------ | ------------------- | --------------------------------------------------------------------------- |
| `Authorization`    | Protected endpoints | `Bearer <access_token>`                                                     |
| `X-Correlation-ID` | No                  | UUID for distributed tracing. Auto-generated if absent. Echoed on response. |
| `Content-Type`     | Request body        | `application/json`                                                          |

## JSON Conventions

- **Property naming:** `snake_case` (e.g., `owner_email`, `provisioned_at`)
- **Null handling:** Null properties are omitted from responses
- **Dates:** ISO 8601 format (`2026-04-01T12:00:00Z`)
- **IDs:** UUID format (`550e8400-e29b-41d4-a716-446655440000`)

## Error Responses

All errors follow a canonical envelope. See [Errors](errors.md) for the full schema and error code reference.

```json
{
  "code": "TENANT_NOT_FOUND",
  "message": "Tenant not found",
  "request_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Modules

| Module       | Prefix                | Status      | Reference                  |
| ------------ | --------------------- | ----------- | -------------------------- |
| Health       | `/health`, `/ready`   | Implemented | [health.md](health.md)     |
| Tenancy      | `/api/v1/tenants`     | Implemented | [tenancy.md](tenancy.md)   |
| Auth         | `/api/v1/auth`        | Implemented | [auth.md](auth.md)         |
| Admin        | `/api/v1/admin`       | Implemented | [admin.md](admin.md)       |
| Profiles     | `/api/v1/profiles`    | Implemented | [profiles.md](profiles.md) |
| Recruitment  | `/api/v1/recruitment` | Planned     | —                          |
| Screening    | `/api/v1/screening`   | Planned     | —                          |
| Matching     | `/api/v1/matching`    | Planned     | —                          |
| HR Workflows | `/api/v1/hr`          | Planned     | —                          |

## Reference Pages

- [Authentication](authentication.md) — JWT Bearer scheme, headers, tenant resolution
- [Errors](errors.md) — Error envelope schema, all error codes
- [SharedKernel](shared-kernel.md) — Domain primitives, events, result types
- [Health](health.md) — Health and readiness probes
- [Tenancy](tenancy.md) — Tenant registration and lookup

## Internal Documentation

For contributor-facing architecture docs (middleware pipeline, configuration, DI registration), see [`docs/internals/`](../internals/README.md).
