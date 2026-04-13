# Catalog Database Design

The Catalog DB is the one shared database on the platform. It stores tenant metadata, routing info, and branding. No user data lives here — that's all in per-tenant databases.

---

## Tables

### tenants

Core tenant identity, routing, ownership, and contact info. This is the hottest table in the catalog — the middleware hits it (via Redis cache) on every request to resolve subdomain → connection string + branding.

| Column            | Type         | Constraints      | Description                                                     |
| ----------------- | ------------ | ---------------- | --------------------------------------------------------------- |
| id                | uuid         | PK               |                                                                 |
| name              | varchar(200) | NOT NULL, UNIQUE | Company display name                                            |
| subdomain         | varchar(63)  | NOT NULL, UNIQUE | DNS label for `{subdomain}.jobsite.com`                         |
| connection_string | varchar(500) | NOT NULL         | Routes to this tenant's isolated PostgreSQL database            |
| status            | varchar(20)  | NOT NULL         | Enum: `Provisioning`, `Active`, `Suspended`, `Deactivated`      |
| owner_name        | varchar(200) | NOT NULL         | Person who registered the tenant; seeded as initial AgencyAdmin |
| owner_email       | varchar(254) | NOT NULL         | Used to seed the first user account in the tenant DB            |
| contact_name      | varchar(200) | NOT NULL         | Receives platform notifications (may differ from owner)         |
| contact_email     | varchar(254) | NOT NULL         | Platform-level communications: incidents, provisioning updates  |
| provisioned_at    | timestamp    | nullable         | Set when the tenant DB is created and migrations complete       |
| deactivated_at    | timestamp    | nullable         | Set when status moves to `Deactivated`                          |
| created_at        | timestamp    | NOT NULL         |                                                                 |
| updated_at        | timestamp    | NOT NULL         | Auto-set on modification                                        |

**Indexes:**

| Name                 | Columns   | Type       | Purpose                                |
| -------------------- | --------- | ---------- | -------------------------------------- |
| ix_tenants_subdomain | subdomain | Unique     | Tenant resolution (middleware, cached) |
| ix_tenants_name      | name      | Unique     | Prevent duplicate company names        |
| ix_tenants_status    | status    | Non-unique | Platform admin filtering by status     |

---

### tenant_brandings

Visual customization for the tenant's portal. One-to-one with `tenants` using a shared primary key. When this row doesn't exist, platform defaults are used. Eager-loaded with the tenant on resolution and cached in Redis, so the JOIN cost is paid once on cache miss.

| Column          | Type          | Constraints         | Description                                             |
| --------------- | ------------- | ------------------- | ------------------------------------------------------- |
| tenant_id       | uuid          | PK, FK → tenants.id | Shared key — also the primary key                       |
| logo_url        | varchar(2048) | nullable            | CDN URL for the company logo                            |
| favicon_url     | varchar(2048) | nullable            | CDN URL for the portal favicon                          |
| primary_color   | varchar(9)    | nullable            | Hex color for buttons, links, accents (e.g., `#1A73E8`) |
| secondary_color | varchar(9)    | nullable            | Hex color for backgrounds, hover states                 |
| tagline         | varchar(500)  | nullable            | Displayed on the login/landing page                     |
| created_at      | timestamp     | NOT NULL            |                                                         |
| updated_at      | timestamp     | NOT NULL            | Auto-set on modification                                |

No additional indexes — accessed only via the FK join on `tenant_id`.

---

## Schema

All tables live under the `catalog` schema to clearly separate them from any tenant data.

## Relationships

```
tenants ||--o| tenant_brandings : "has (optional, one-to-one)"
```

## Tenant Status Lifecycle

```
Provisioning → Active → Suspended → Active (reactivated)
                     → Deactivated (terminal)
```

- **Provisioning**: Database is being created, migrations running. Not accessible.
- **Active**: Tenant is live. Users can log in at `{subdomain}.jobsite.com`.
- **Suspended**: Temporarily blocked (e.g., payment failure). Data preserved, access denied.
- **Deactivated**: Permanently shut down by platform admin. `deactivated_at` is set.

## Caching Strategy

Tenant resolution is the hottest path — every inbound request needs it. The full tenant object (including branding) is cached in Redis on first lookup, keyed by subdomain. Cache is invalidated only when tenant metadata or branding is updated, which is rare.

## Provisioning Flow

```
1. POST /api/tenants/register with company name, subdomain, owner info
2. Tenant row created (status = Provisioning)
3. Contact fields defaulted to owner name/email
4. New PostgreSQL database provisioned
5. EF Core migrations run against new database
6. Initial AgencyAdmin user seeded in tenant DB (using owner_email)
7. Tenant status set to Active, provisioned_at timestamped
8. Tenant is live at {subdomain}.jobsite.com
```

## Design Decisions

**Flat contact fields instead of a contacts table.** Most tenants need one contact person. A separate table with role-based routing (billing, technical, etc.) is premature — extract it when you actually build multi-contact notifications.

**Separate branding table instead of columns on tenants.** Branding will grow (custom fonts, email headers, theme variants). The JOIN cost is negligible because the entire tenant + branding object is cached in Redis on resolution. Keeps the tenant table focused on identity and routing.

**Enums stored as strings.** Readable in the database, easy to query without a lookup table. The small storage cost is irrelevant at the scale of the catalog (hundreds to low thousands of rows).

**Shared primary key for branding.** `tenant_id` is both the PK and FK on `tenant_brandings`. This enforces the one-to-one relationship at the database level and avoids a redundant `id` column.
