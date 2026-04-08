# Matching Module — Database Design

> Schema: `matching` · Per-tenant database

## Overview

The Matching module aggregates screening and assessment scores into weighted composite scores, ranks candidates per job posting, and generates shortlists for hiring manager review. All three tables use cross-schema foreign keys to `recruitment.applications`, `recruitment.job_postings`, and `auth.users`.

---

## Tables

### candidate_matches

One row per application that passes screening. Uses a **shared primary key** with `recruitment.applications` — `application_id` is both the PK and FK.

| Column                    | Type           | Nullable | Default | Description                                          |
| ------------------------- | -------------- | -------- | ------- | ---------------------------------------------------- |
| `application_id`          | `uuid`         | NOT NULL | —       | PK / FK to `recruitment.applications.id`             |
| `job_posting_id`          | `uuid`         | NOT NULL | —       | FK to `recruitment.job_postings.id`                  |
| `applicant_user_id`       | `uuid`         | NOT NULL | —       | FK to `auth.users.id`                                |
| `screening_score`         | `decimal(5,2)` | NOT NULL | —       | Deterministic screening overall score (0–100)        |
| `assessment_score`        | `decimal(5,2)` | NULL     | —       | Assessment score; NULL until assessment completes    |
| `composite_score`         | `decimal(5,2)` | NOT NULL | —       | Weighted composite of screening + assessment         |
| `match_strength`          | `varchar(20)`  | NOT NULL | —       | Strong / Good / Moderate / Weak                      |
| `rank`                    | `int`          | NULL     | —       | Rank within job posting (1-based); NULL until ranked |
| `screening_completed_at`  | `timestamptz`  | NOT NULL | —       | When screening completed                             |
| `assessment_completed_at` | `timestamptz`  | NULL     | —       | When assessment completed                            |
| `created_at`              | `timestamptz`  | NOT NULL | `NOW()` | Row creation timestamp                               |
| `updated_at`              | `timestamptz`  | NOT NULL | `NOW()` | Last update timestamp                                |

**Indexes:**

- `ix_candidate_matches_job_posting_id` on `job_posting_id`
- `ix_candidate_matches_applicant_user_id` on `applicant_user_id`
- `ix_candidate_matches_composite_score` on `composite_score`
- `ix_candidate_matches_match_strength` on `match_strength`

**CHECK constraints:**

- `chk_matches_match_strength`: `match_strength IS NULL OR match_strength IN ('Strong', 'Good', 'Moderate', 'Weak')`

**Cross-schema FKs (added via raw SQL in migration):**

- `application_id` → `recruitment.applications(id)`
- `job_posting_id` → `recruitment.job_postings(id)`
- `applicant_user_id` → `auth.users(id)`

---

### shortlists

Per-job-posting shortlist of top candidates. Aggregate root that transitions from Draft → Finalized.

| Column             | Type           | Nullable | Default             | Description                               |
| ------------------ | -------------- | -------- | ------------------- | ----------------------------------------- |
| `id`               | `uuid`         | NOT NULL | `gen_random_uuid()` | Primary key                               |
| `job_posting_id`   | `uuid`         | NOT NULL | —                   | FK to `recruitment.job_postings.id`       |
| `status`           | `varchar(20)`  | NOT NULL | —                   | Draft / Finalized                         |
| `generated_by`     | `varchar(100)` | NOT NULL | —                   | "Algorithm" or user identifier            |
| `total_candidates` | `int`          | NOT NULL | —                   | Count of active (non-removed) candidates  |
| `finalized_at`     | `timestamptz`  | NULL     | —                   | When the shortlist was finalized          |
| `finalized_by`     | `uuid`         | NULL     | —                   | FK to `auth.users.id`; user who finalized |
| `created_at`       | `timestamptz`  | NOT NULL | `NOW()`             | Row creation timestamp                    |
| `updated_at`       | `timestamptz`  | NOT NULL | `NOW()`             | Last update timestamp                     |

**Indexes:**

- `ix_shortlists_job_posting_id` on `job_posting_id`
- `ix_shortlists_status` on `status`

**CHECK constraints:**

- `chk_shortlists_status`: `status IN ('Draft', 'Finalized')`

**Cross-schema FKs (added via raw SQL in migration):**

- `job_posting_id` → `recruitment.job_postings(id)`
- `finalized_by` → `auth.users(id)`

---

### shortlist_candidates

Candidates on a shortlist. Unique constraint prevents the same candidate from appearing twice on the same shortlist. Supports soft removal via `removed_at`.

| Column              | Type           | Nullable | Default             | Description                                     |
| ------------------- | -------------- | -------- | ------------------- | ----------------------------------------------- |
| `id`                | `uuid`         | NOT NULL | `gen_random_uuid()` | Primary key                                     |
| `shortlist_id`      | `uuid`         | NOT NULL | —                   | FK to `matching.shortlists.id` (cascade delete) |
| `application_id`    | `uuid`         | NOT NULL | —                   | FK to `recruitment.applications.id`             |
| `applicant_user_id` | `uuid`         | NOT NULL | —                   | FK to `auth.users.id`                           |
| `composite_score`   | `decimal(5,2)` | NOT NULL | —                   | Composite score at time of shortlisting         |
| `rank`              | `int`          | NOT NULL | —                   | Position in shortlist (1-based)                 |
| `source`            | `varchar(20)`  | NOT NULL | —                   | Algorithm / Manual                              |
| `status`            | `varchar(20)`  | NOT NULL | `'Pending'`         | Pending / Approved / Rejected                   |
| `added_at`          | `timestamptz`  | NOT NULL | —                   | When added to shortlist                         |
| `removed_at`        | `timestamptz`  | NULL     | —                   | Soft removal timestamp                          |
| `created_at`        | `timestamptz`  | NOT NULL | `NOW()`             | Row creation timestamp                          |
| `updated_at`        | `timestamptz`  | NOT NULL | `NOW()`             | Last update timestamp                           |

**Indexes:**

- `uq_shortlist_candidates_shortlist_app` UNIQUE on `(shortlist_id, application_id)`
- `ix_shortlist_candidates_application_id` on `application_id`

**CHECK constraints:**

- `chk_shortlist_candidates_source`: `source IN ('Algorithm', 'Manual')`
- `chk_shortlist_candidates_status`: `status IN ('Pending', 'Approved', 'Rejected')`

**Cross-schema FKs (added via raw SQL in migration):**

- `application_id` → `recruitment.applications(id)`
- `applicant_user_id` → `auth.users(id)`

---

## Entity Relationships

```
recruitment.applications ──1:0..1──> matching.candidate_matches  (shared PK)
recruitment.job_postings ──1:N────> matching.candidate_matches
recruitment.job_postings ──1:N────> matching.shortlists
matching.shortlists      ──1:N────> matching.shortlist_candidates (cascade)
recruitment.applications ──1:N────> matching.shortlist_candidates
```

## Score Computation

Composite scores use tenant-configurable weights from `admin.company_settings.matching_settings` JSONB:

```
composite = (screening_score × screening_weight / total_weight)
          + (assessment_score × assessment_weight / total_weight)
```

Default weights: screening=60, assessment=40. When assessment is not yet available, composite = screening score.

## Match Strength Thresholds

| Range  | Strength |
| ------ | -------- |
| 80–100 | Strong   |
| 60–79  | Good     |
| 40–59  | Moderate |
| 0–39   | Weak     |
