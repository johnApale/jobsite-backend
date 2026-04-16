# Tenancy Module

The Tenancy module manages tenant provisioning and lookup. It operates on the shared **Catalog database** and is not subject to tenant resolution (all tenancy routes bypass the `TenantResolutionMiddleware`).

**Base path:** `/api/v1/tenants`
**Tag:** `Tenants`

## Endpoints

### `GET /api/v1/tenants/{id}`

Retrieve tenant metadata and branding by tenant ID.

**Authentication:** None (will require `PlatformAdmin` role in production)
**Tenant resolution:** Bypassed

#### Path Parameters

| Parameter | Type   | Description |
| --------- | ------ | ----------- |
| `id`      | `uuid` | Tenant ID   |

#### Response — `200 OK`

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Staffing",
  "subdomain": "acme",
  "status": "Active",
  "owner_name": "Jane Smith",
  "owner_email": "jane@acmestaffing.com",
  "contact_name": "Jane Smith",
  "contact_email": "jane@acmestaffing.com",
  "provisioned_at": "2026-03-15T10:30:00Z",
  "branding": {
    "logo_url": "https://cdn.jobsite.com/acme/logo.png",
    "favicon_url": "https://cdn.jobsite.com/acme/favicon.ico",
    "primary_color": "#1E40AF",
    "secondary_color": "#F59E0B",
    "tagline": "Connecting talent with opportunity"
  }
}
```

#### Response — `404 Not Found`

```json
{
  "code": "TENANT_NOT_FOUND",
  "message": "Tenant not found",
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

---

### `POST /api/v1/tenants/register`

Register a new tenant. Creates the tenant in `Provisioning` status and triggers asynchronous database provisioning.

**Authentication:** API key via `X-Api-Key` header
**Tenant resolution:** Bypassed

#### Request Headers

| Header      | Required | Description                         |
| ----------- | -------- | ----------------------------------- |
| `X-Api-Key` | Yes      | Platform API key from configuration |

#### Request Body

```json
{
  "name": "Acme Staffing",
  "subdomain": "acme",
  "owner_name": "Jane Smith",
  "owner_email": "jane@acmestaffing.com"
}
```

| Field         | Type     | Required | Description                             |
| ------------- | -------- | -------- | --------------------------------------- |
| `name`        | `string` | Yes      | Company display name                    |
| `subdomain`   | `string` | Yes      | DNS label for `{subdomain}.jobsite.com` |
| `owner_name`  | `string` | Yes      | Person who registered the tenant        |
| `owner_email` | `string` | Yes      | Seeded as the first `AgencyAdmin` user  |

#### Response — `201 Created`

```http
HTTP/1.1 201 Created
Location: /api/v1/tenants/550e8400-e29b-41d4-a716-446655440000
```

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Staffing",
  "subdomain": "acme",
  "status": "Provisioning",
  "owner_name": "Jane Smith",
  "owner_email": "jane@acmestaffing.com",
  "contact_name": "Jane Smith",
  "contact_email": "jane@acmestaffing.com"
}
```

Note: `provisioned_at`, `deactivated_at`, and `branding` are null (omitted from response) for newly registered tenants.

#### Response — `400 Bad Request`

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": {
    "subdomain": "Subdomain is already taken",
    "owner_email": "Must be a valid email address"
  },
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

#### Response — `401 Unauthorized`

Returned when the `X-Api-Key` header is missing.

```json
{
  "code": "UNAUTHORIZED",
  "message": "Missing X-Api-Key header."
}
```

#### Response — `403 Forbidden`

Returned when the `X-Api-Key` header value is invalid.

```json
{
  "code": "FORBIDDEN",
  "message": "Invalid API key."
}
```

## Schemas

### `TenantResponse`

| Field            | Type                     | Nullable | Description                              |
| ---------------- | ------------------------ | -------- | ---------------------------------------- |
| `id`             | `uuid`                   | No       | Tenant identifier                        |
| `name`           | `string`                 | No       | Company display name                     |
| `subdomain`      | `string`                 | No       | DNS label                                |
| `status`         | `string`                 | No       | Current tenant status (see below)        |
| `owner_name`     | `string`                 | No       | Tenant owner name                        |
| `owner_email`    | `string`                 | No       | Tenant owner email                       |
| `contact_name`   | `string`                 | No       | Primary contact name                     |
| `contact_email`  | `string`                 | No       | Primary contact email                    |
| `provisioned_at` | `datetime`               | Yes      | When the tenant database was provisioned |
| `deactivated_at` | `datetime`               | Yes      | When the tenant was deactivated          |
| `branding`       | `TenantBrandingResponse` | Yes      | Branding configuration                   |

### `TenantBrandingResponse`

| Field             | Type     | Nullable | Description                 |
| ----------------- | -------- | -------- | --------------------------- |
| `logo_url`        | `string` | Yes      | Company logo URL            |
| `favicon_url`     | `string` | Yes      | Browser favicon URL         |
| `primary_color`   | `string` | Yes      | Primary brand color (hex)   |
| `secondary_color` | `string` | Yes      | Secondary brand color (hex) |
| `tagline`         | `string` | Yes      | Company tagline             |

### `RegisterTenantRequest`

| Field         | Type     | Required | Description                             |
| ------------- | -------- | -------- | --------------------------------------- |
| `name`        | `string` | Yes      | Company display name                    |
| `subdomain`   | `string` | Yes      | DNS label for `{subdomain}.jobsite.com` |
| `owner_name`  | `string` | Yes      | Person who registered the tenant        |
| `owner_email` | `string` | Yes      | Seeded as the first `AgencyAdmin` user  |

## Tenant Status

| Status               | Description                                      |
| -------------------- | ------------------------------------------------ |
| `Provisioning`       | Tenant registered, database being created        |
| `Active`             | Tenant fully operational                         |
| `Suspended`          | Temporarily disabled (billing, policy violation) |
| `Deactivated`        | Permanently disabled                             |
| `ProvisioningFailed` | Database creation failed (see logs for details)  |

Status values use `PascalCase` to match the database CHECK constraint.

## Provisioning Flow

When a tenant registers via `POST /api/v1/tenants/register`, the following sequence occurs:

```
POST /register → Tenant created (Provisioning) → SaveChanges → ProvisionAsync → CREATE DATABASE → Active → TenantProvisionedEvent
                                                                                             ╲
                                                                                              → ProvisioningFailed (on error)
```

1. **Registration**: `TenantService.RegisterAsync()` creates a `Tenant` entity with status `Provisioning` and calls `SaveChangesAsync()` on the catalog database.
2. **Provisioning**: `ITenantProvisioner.ProvisionAsync()` is called after the tenant is persisted:
   - Builds a database name: `jobsite_tenant_{subdomain}` (sanitized to alphanumeric + underscore).
   - Executes `CREATE DATABASE "jobsite_tenant_{subdomain}"` via a raw SQL connection to the catalog PostgreSQL server.
   - Builds a connection string for the new database using the catalog server's host, port, and credentials.
   - Updates the tenant: `ConnectionString`, `Status = Active`, `ProvisionedAt = DateTime.UtcNow`.
3. **Event publishing**: After successful provisioning, `TenantProvisionedEvent` is dispatched via the in-process domain event bus. This triggers the Admin module's `TenantProvisionedHandler` to seed default `CompanySettings` for the new tenant.
4. **On failure**: If any step fails, the tenant's status is set to `ProvisioningFailed` and the error is logged. The tenant middleware will reject requests to `ProvisioningFailed` tenants with a 403.

Tenants in `Provisioning` or `ProvisioningFailed` status cannot be accessed via subdomain — the `TenantResolutionMiddleware` only allows `Active` tenants through.

---

## Platform Admin Endpoints

Platform admin endpoints provide system-wide tenant management. They operate on the shared **Catalog database** and require the `RequirePlatformAdmin` authorization policy.

**Base path:** `/api/v1/platform/tenants`
**Tag:** `PlatformAdmin`

### `POST /api/v1/platform/tenants`

Register a new tenant via JWT authentication. Creates the tenant in `Provisioning` status and triggers asynchronous database provisioning. This is an alternative to `POST /api/v1/tenants/register` (which uses API key auth) — both call the same service.

**Authorization:** `RequirePlatformAdmin`

#### Request Body

Same as [`RegisterTenantRequest`](#registertenantrequest).

#### Response — `201 Created`

```http
HTTP/1.1 201 Created
Location: /api/v1/platform/tenants/550e8400-e29b-41d4-a716-446655440000
```

Returns a `TenantResponse` with `status: "Provisioning"`.

#### Errors

| Code               | Status | Condition                             |
| ------------------ | ------ | ------------------------------------- |
| `VALIDATION_ERROR` | 400    | Missing fields or duplicate subdomain |
| `UNAUTHORIZED`     | 401    | Missing or invalid JWT                |
| `FORBIDDEN`        | 403    | JWT role is not PlatformAdmin         |

---

### `GET /api/v1/platform/tenants`

List all tenants with optional filters and cursor-based pagination.

**Authorization:** `RequirePlatformAdmin`

#### Query Parameters

| Parameter   | Type     | Required | Description                                           |
| ----------- | -------- | -------- | ----------------------------------------------------- |
| `status`    | `string` | No       | Filter by tenant status (e.g., `Active`, `Suspended`) |
| `search`    | `string` | No       | Search by tenant name or subdomain                    |
| `cursor`    | `string` | No       | Opaque cursor from previous response's `next_cursor`  |
| `page_size` | `int`    | No       | Results per page (default: 20)                        |

#### Response — `200 OK`

```json
{
  "items": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Acme Staffing",
      "subdomain": "acme",
      "status": "Active",
      "owner_name": "Jane Smith",
      "owner_email": "jane@acmestaffing.com",
      "contact_name": "Jane Smith",
      "contact_email": "jane@acmestaffing.com",
      "provisioned_at": "2026-03-15T10:30:00Z",
      "branding": null
    }
  ],
  "next_cursor": "660e8400-e29b-41d4-a716-446655440000",
  "has_more": true
}
```

---

### `GET /api/v1/platform/tenants/{id}`

Retrieve full tenant metadata by ID for platform administration.

**Authorization:** `RequirePlatformAdmin`

#### Path Parameters

| Parameter | Type   | Description |
| --------- | ------ | ----------- |
| `id`      | `uuid` | Tenant ID   |

#### Response — `200 OK`

Same shape as `TenantResponse` schema above.

#### Response — `404 Not Found`

```json
{
  "code": "TENANT_NOT_FOUND",
  "message": "Tenant not found",
  "request_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

---

### `POST /api/v1/platform/tenants/{id}/suspend`

Suspend an active tenant, preventing all access.

**Authorization:** `RequirePlatformAdmin`

#### Path Parameters

| Parameter | Type   | Description |
| --------- | ------ | ----------- |
| `id`      | `uuid` | Tenant ID   |

#### Response — `200 OK`

Returns the updated `TenantResponse` with `status: "Suspended"` and `deactivated_at` set.

#### Errors

| Code               | Status | Condition                        |
| ------------------ | ------ | -------------------------------- |
| `TENANT_NOT_FOUND` | 404    | Tenant does not exist            |
| `INVALID_REQUEST`  | 400    | Tenant is not in `Active` status |

---

### `POST /api/v1/platform/tenants/{id}/reactivate`

Reactivate a previously suspended tenant.

**Authorization:** `RequirePlatformAdmin`

#### Path Parameters

| Parameter | Type   | Description |
| --------- | ------ | ----------- |
| `id`      | `uuid` | Tenant ID   |

#### Response — `200 OK`

Returns the updated `TenantResponse` with `status: "Active"` and `deactivated_at` cleared.

#### Errors

| Code               | Status | Condition                           |
| ------------------ | ------ | ----------------------------------- |
| `TENANT_NOT_FOUND` | 404    | Tenant does not exist               |
| `INVALID_REQUEST`  | 400    | Tenant is not in `Suspended` status |
