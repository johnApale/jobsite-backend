# Auth Module — Database Design

Authentication and token management for the tenant database. Every user account, credential, and session token lives here — scoped to a single tenant's database. No auth data in the catalog.

This is custom auth — not ASP.NET Identity. Identity Framework's rigid table schema and UserManager abstractions fight against database-per-tenant multi-tenancy. Custom auth gives full control over the schema, token flow, and how tenant resolution integrates with authentication.

Supports email/password login and OAuth (Google, Apple, Facebook). Users can have one or both — an applicant might sign up with Google, a staff member might use email/password only, and either could link additional providers later.

---

## Tables

### users

Every person who can log into this tenant's portal. Staff (recruiters, hiring managers, interviewers) are created via admin invite. Applicants self-register (via email/password or OAuth). The initial AgencyAdmin is seeded during tenant provisioning from the owner email in the catalog.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| email | varchar(254) | NOT NULL, UNIQUE | Login identifier. Uniqueness is per-tenant (per-database), not global |
| password_hash | varchar(200) | nullable | BCrypt hash. NULL for OAuth-only users. Set on registration, invitation activation, or when a user adds a password to their OAuth account |
| email_verified | boolean | NOT NULL, DEFAULT false | True if verified via email confirmation or if the user registered through an OAuth provider that supplies a verified email |
| role | varchar(20) | NOT NULL | Enum: `Applicant`, `Recruiter`, `HiringManager`, `Interviewer`, `AgencyAdmin` |
| status | varchar(20) | NOT NULL | Enum: `Active`, `Invited`, `Deactivated` |
| first_name | varchar(100) | NOT NULL | |
| last_name | varchar(100) | NOT NULL | |
| avatar_url | varchar(2048) | nullable | Profile picture URL. Initially populated from OAuth provider if available |
| invited_by | uuid | nullable, FK → users.id | The admin/manager who created this account. NULL for self-registered applicants and the seeded AgencyAdmin |
| last_login_at | timestamp | nullable | Updated on each successful authentication (email/password or OAuth) |
| deactivated_at | timestamp | nullable | Set when status moves to `Deactivated` |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_users_email | email | Unique | Login lookup, duplicate prevention, OAuth email matching |
| ix_users_role | role | Non-unique | Filtering users by role (admin panels, assignment queries) |
| ix_users_status | status | Non-unique | Active user filtering, admin views |
| ix_users_invited_by | invited_by | Non-unique | "Who did this admin invite?" queries |

---

### user_external_logins

Maps users to their OAuth provider identities. A user can have multiple linked providers (Google + Apple, etc.). The provider's subject ID is the stable identifier — not the email, which can change on the provider side.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| user_id | uuid | NOT NULL, FK → users.id | The local user this external identity belongs to |
| provider | varchar(20) | NOT NULL | Enum: `Google`, `Apple`, `Facebook` |
| provider_subject_id | varchar(200) | NOT NULL | The `sub` claim from the provider's ID token. Stable, unique per user per provider |
| provider_email | varchar(254) | nullable | Email from the provider at time of linking. Informational — not used for matching after initial link |
| provider_display_name | varchar(200) | nullable | Name from the provider at time of linking. Informational only |
| linked_at | timestamp | NOT NULL | When this provider was linked to the user account |
| created_at | timestamp | NOT NULL | |

**Constraints:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| uq_external_logins_provider_subject | provider, provider_subject_id | Unique | One local account per provider identity. Prevents the same Google account from being linked to two users |
| uq_external_logins_user_provider | user_id, provider | Unique | One link per provider per user. A user can't link two Google accounts |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_external_logins_provider_subject | provider, provider_subject_id | Unique | OAuth login lookup: "which user owns this Google sub?" |
| ix_external_logins_user_id | user_id | Non-unique | "What providers does this user have linked?" |

---

### refresh_tokens

Supports JWT refresh token rotation with replay detection. Each login session starts a token family. On rotation, the old token is revoked and a new one issued in the same family. If a revoked token is ever presented again, the entire family is revoked — this catches token theft.

Works identically for email/password and OAuth login sessions — both produce JWTs and refresh tokens through the same flow.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| user_id | uuid | NOT NULL, FK → users.id | The user this token belongs to |
| token_hash | varchar(200) | NOT NULL, UNIQUE | SHA-256 hash of the refresh token. Never store the raw token |
| family_id | uuid | NOT NULL | Groups all tokens from one login session. Enables family-wide revocation on replay |
| is_revoked | boolean | NOT NULL, DEFAULT false | Set to true on rotation or explicit logout |
| expires_at | timestamp | NOT NULL | Absolute expiration — token is invalid after this regardless of revocation status |
| revoked_at | timestamp | nullable | When this specific token was revoked (rotation or logout) |
| replaced_by_id | uuid | nullable, FK → refresh_tokens.id | Points to the token that replaced this one on rotation. NULL if this is the current active token |
| created_at | timestamp | NOT NULL | Issued at — also serves as the rotation timestamp for the previous token |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_refresh_tokens_token_hash | token_hash | Unique | Token lookup on refresh requests |
| ix_refresh_tokens_user_id | user_id | Non-unique | "Revoke all sessions for this user" |
| ix_refresh_tokens_family_id | family_id | Non-unique | Family-wide revocation on replay detection |
| ix_refresh_tokens_expires_at | expires_at | Non-unique | Cleanup job — bulk delete expired tokens |

---

## Schema

All Auth module tables live under the `auth` schema within the tenant database. Each module gets its own schema to enforce ownership boundaries at the database level.

```sql
CREATE SCHEMA IF NOT EXISTS auth;
```

---

## Relationships

```
users ||--o{ user_external_logins : "has many (one per OAuth provider)"
users ||--o{ refresh_tokens : "has many (one per login session, chained on rotation)"
users |o--o{ users : "invited_by (self-referencing, nullable)"
refresh_tokens |o--o| refresh_tokens : "replaced_by_id (self-referencing chain)"
```

---

## User Status Lifecycle

```
Invited → Active → Deactivated
Active (self-registered, email/password or OAuth) → Deactivated
```

- **Invited**: Admin created the account. `password_hash` is NULL and no external logins exist. User must complete activation (set password or link an OAuth provider) before they can log in.
- **Active**: User can authenticate. Includes self-registered applicants (email/password or OAuth) and activated staff.
- **Deactivated**: Account disabled by admin. `deactivated_at` is set. Existing refresh tokens should be revoked. User cannot log in — including via OAuth.

No "Suspended" state at the user level — suspension is a tenant-level concept (handled in the catalog DB). If a tenant is suspended, the middleware blocks all requests before auth is even reached.

---

## OAuth Login Flow

```
1. User clicks "Sign in with Google" on {subdomain}.djobsite.com
2. Frontend redirects to Google's OAuth consent screen
   - redirect_uri includes the tenant subdomain so we know where to route back
3. Google redirects back with authorization code
4. Backend exchanges code for Google ID token
5. Extract provider_subject_id (sub claim) and email from ID token
6. Lookup: SELECT * FROM auth.user_external_logins
          WHERE provider = 'Google' AND provider_subject_id = {sub}

   CASE A — Existing linked account:
     → Load the user, check status is Active
     → Issue JWT + refresh token (same as email/password login from here)

   CASE B — No linked account, but email matches an existing user:
     → Link the Google identity to the existing user (create user_external_logins row)
     → Set email_verified = true (Google verified the email)
     → Issue JWT + refresh token

   CASE C — No linked account, no matching email (new user):
     → Create new user (role = Applicant, status = Active, password_hash = NULL)
     → Create user_external_logins row
     → Set email_verified = true
     → Issue JWT + refresh token
```

**Auto-linking on email match (Case B) is only safe when the OAuth provider confirms the email is verified.** Google and Apple always verify emails. Facebook does not always — for Facebook, if the email matches but isn't provider-verified, prompt the user to confirm ownership (e.g., enter their existing password) before linking.

---

## Refresh Token Rotation Flow

```
1. User logs in (email/password or OAuth) → JWT access token + refresh token issued
2. New refresh_tokens row: family_id = new UUID, is_revoked = false
3. Access token expires → client sends refresh token
4. Server finds token by hash, validates: not revoked, not expired
5. Old token marked is_revoked = true, revoked_at = now
6. New token created in same family_id, old token's replaced_by_id points to new token
7. New JWT access token + new refresh token returned

Replay detection:
8. Attacker tries to use the old (revoked) token
9. Server sees is_revoked = true → revoke ALL tokens in that family_id
10. Legitimate user's current token is now also revoked — they must re-login
11. Attacker gets nothing
```

---

## Cleanup

Expired refresh tokens should be purged periodically. A background job can delete all rows where `expires_at < now()` — the `ix_refresh_tokens_expires_at` index supports this efficiently. Frequency: daily is fine, these rows are small.

---

## Design Decisions

**Custom auth instead of ASP.NET Identity.** Identity Framework owns its table schema (`AspNetUsers`, `AspNetRoles`, `AspNetUserLogins`, etc.) and fights against database-per-tenant patterns. Its `UserManager<T>` assumes a single database context. Custom auth gives full control over multi-tenant routing, schema design, and token management without working around framework assumptions.

**No tenant ID column.** The database *is* the tenant boundary. Each tenant has their own PostgreSQL database. Adding a tenant ID to every row would be redundant — there's no other tenant's data to filter against. Tenant identity is resolved by the middleware (subdomain → connection string from catalog DB), and from that point on, every query runs against the correct database.

**Nullable `password_hash`.** OAuth-only users don't have a password. Making this nullable instead of storing a dummy hash is honest — you can check `password_hash IS NULL` to determine if a user can do email/password login, and prompt them to set a password if they want to add that option.

**`email_verified` flag.** OAuth providers handle email verification on their end. When a user signs up via Google, we can trust the email is verified. For email/password registration, this defaults to false until confirmed. This flag is useful for: gating certain actions behind verified email, deciding whether auto-linking is safe during OAuth flows, and avoiding sending sensitive info to unverified addresses.

**Provider subject ID, not email, as the stable OAuth key.** Emails can change on the provider side. The `sub` claim from an OAuth ID token is permanent and unique per user per provider. The composite unique constraint on `(provider, provider_subject_id)` is the lookup key for OAuth login.

**Auto-link with safety guard.** When an OAuth login matches an existing user by email, we auto-link — but only if the provider confirms the email is verified. This is safe for Google and Apple. Facebook requires an extra confirmation step because it doesn't always verify emails. This prevents an attacker from creating a Facebook account with someone else's email and hijacking their account.

**One link per provider per user, one user per provider identity.** The two unique constraints on `user_external_logins` enforce this bidirectionally. A user can't link two Google accounts (which would they use to log in?), and a Google account can't be linked to two users (who should we authenticate?).

**Single role column instead of a roles join table.** The README defines users as having exactly one role. A join table adds complexity for a many-to-many relationship that doesn't exist yet. If multi-role support is needed later, extract to `user_roles` — but don't build it speculatively.

**`Invited` status instead of a separate invitations table.** An invitation is just an inactive user account waiting to be activated. Tracking this as a status on the user row avoids a parallel table that would need to be synced with users on activation. The `invited_by` FK gives you the audit trail.

**Hashed refresh tokens, not raw.** Same principle as passwords — if the database is compromised, the attacker can't use the tokens. SHA-256 is sufficient here (unlike passwords, refresh tokens are high-entropy random values that don't need bcrypt's slow hashing).

**Family-based rotation with `replaced_by_id` chain.** `family_id` enables bulk revocation. `replaced_by_id` gives you a linked list of the rotation history if you ever need to debug a token chain. Both are cheap to store and query.

**Per-module schemas.** Using `auth.*` instead of `public.*` makes ownership explicit. When another module needs user data, it should go through a shared interface in code — not query `auth.users` directly. The schema boundary is a visible reminder of that rule.

**No `updated_at` on refresh_tokens or user_external_logins.** Both are append-mostly. Refresh tokens are created, potentially revoked (`revoked_at` captures that), then deleted. External logins are created and rarely modified. Adding `updated_at` would just duplicate existing timestamps or track non-events.
