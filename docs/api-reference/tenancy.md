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
    "logo_url": "https://cdn.djobsite.com/acme/logo.png",
    "favicon_url": "https://cdn.djobsite.com/acme/favicon.ico",
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

**Authentication:** None (public registration)
**Tenant resolution:** Bypassed

#### Request Body

```json
{
  "name": "Acme Staffing",
  "subdomain": "acme",
  "owner_name": "Jane Smith",
  "owner_email": "jane@acmestaffing.com"
}
```

| Field         | Type     | Required | Description                              |
| ------------- | -------- | -------- | ---------------------------------------- |
| `name`        | `string` | Yes      | Company display name                     |
| `subdomain`   | `string` | Yes      | DNS label for `{subdomain}.djobsite.com` |
| `owner_name`  | `string` | Yes      | Person who registered the tenant         |
| `owner_email` | `string` | Yes      | Seeded as the first `AgencyAdmin` user   |

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

| Field         | Type     | Required | Description                              |
| ------------- | -------- | -------- | ---------------------------------------- |
| `name`        | `string` | Yes      | Company display name                     |
| `subdomain`   | `string` | Yes      | DNS label for `{subdomain}.djobsite.com` |
| `owner_name`  | `string` | Yes      | Person who registered the tenant         |
| `owner_email` | `string` | Yes      | Seeded as the first `AgencyAdmin` user   |

## Tenant Status

| Status         | Description                                      |
| -------------- | ------------------------------------------------ |
| `Provisioning` | Tenant registered, database being created        |
| `Active`       | Tenant fully operational                         |
| `Suspended`    | Temporarily disabled (billing, policy violation) |
| `Deactivated`  | Permanently disabled                             |

Status values use `PascalCase` to match the database CHECK constraint.
