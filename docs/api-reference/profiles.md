# Profiles Module API

## Base Path

```
/api/v1/profiles
```

All profiles endpoints require **tenant resolution** (subdomain in `Host` header) and **JWT authentication**.

---

## Profile Endpoints

### Get My Profile

Retrieve the authenticated user's applicant profile.

```
GET /api/v1/profiles/me
```

**Authorization:** Any authenticated user

**Response:** `200 OK`

```json
{
  "user_id": "550e8400-e29b-41d4-a716-446655440000",
  "first_name": "John",
  "last_name": "Doe",
  "phone": "+639171234567",
  "city": "Manila",
  "country": "Philippines",
  "skills": [
    {
      "name": "C#",
      "level": "Advanced",
      "years": 7
    },
    {
      "name": "PostgreSQL",
      "level": "Intermediate",
      "years": 3
    }
  ],
  "social_links": {
    "linked_in": "https://linkedin.com/in/johndoe",
    "git_hub": "https://github.com/johndoe",
    "portfolio": "https://johndoe.dev"
  },
  "documents": [
    {
      "type": "CoverLetter",
      "url": "/uploads/documents/cover-letter.pdf",
      "filename": "cover-letter.pdf",
      "uploaded_at": "2026-04-01T12:00:00Z"
    }
  ],
  "profile_completed_at": null,
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Errors:**

| Code                | Status | Condition                                    |
| ------------------- | ------ | -------------------------------------------- |
| `PROFILE_NOT_FOUND` | 404    | No profile exists for the authenticated user |
| `UNAUTHORIZED`      | 401    | Missing or invalid JWT                       |

---

### Create My Profile

Create a new applicant profile for the authenticated user.

```
POST /api/v1/profiles/me
```

**Authorization:** Any authenticated user

**Request Body:**

```json
{
  "first_name": "John",
  "last_name": "Doe",
  "phone": "+639171234567",
  "city": "Manila",
  "country": "Philippines",
  "skills": [
    {
      "name": "C#",
      "level": "Advanced",
      "years": 7
    }
  ],
  "social_links": {
    "linked_in": "https://linkedin.com/in/johndoe",
    "git_hub": "https://github.com/johndoe"
  },
  "documents": []
}
```

**Validation Rules:**

| Field            | Rule                                                                  |
| ---------------- | --------------------------------------------------------------------- |
| `first_name`     | Required, max 100 characters                                          |
| `last_name`      | Required, max 100 characters                                          |
| `phone`          | Optional, max 20 characters                                           |
| `city`           | Optional, max 100 characters                                          |
| `country`        | Optional, max 100 characters                                          |
| `skills[].name`  | Required when skills provided, max 100 characters                     |
| `skills[].level` | Optional; must be `Beginner`, `Intermediate`, `Advanced`, or `Expert` |
| `skills[].years` | Optional; must be non-negative                                        |

**Response:** `201 Created` — Returns the created profile (same shape as GET).

**Errors:**

| Code                     | Status | Condition                              |
| ------------------------ | ------ | -------------------------------------- |
| `PROFILE_ALREADY_EXISTS` | 409    | A profile already exists for this user |
| `VALIDATION_ERROR`       | 400    | Request body fails validation          |
| `UNAUTHORIZED`           | 401    | Missing or invalid JWT                 |

---

### Update My Profile

Partially update the authenticated user's profile using JSON merge patch semantics. Only non-null fields in the request body are applied.

```
PATCH /api/v1/profiles/me
```

**Authorization:** Any authenticated user

**Request Body:**

All fields are optional. Only provided (non-null) fields are updated. Collection fields (`skills`, `documents`) replace the entire list when provided.

```json
{
  "first_name": "Jonathan",
  "city": "Cebu",
  "skills": [
    {
      "name": "C#",
      "level": "Expert",
      "years": 8
    },
    {
      "name": "React",
      "level": "Intermediate",
      "years": 2
    }
  ]
}
```

**Validation Rules:**

| Field            | Rule                                                                  |
| ---------------- | --------------------------------------------------------------------- |
| `first_name`     | Non-empty when provided, max 100 characters                           |
| `last_name`      | Non-empty when provided, max 100 characters                           |
| `phone`          | Max 20 characters                                                     |
| `city`           | Max 100 characters                                                    |
| `country`        | Max 100 characters                                                    |
| `skills[].name`  | Required when skills provided, max 100 characters                     |
| `skills[].level` | Optional; must be `Beginner`, `Intermediate`, `Advanced`, or `Expert` |
| `skills[].years` | Optional; must be non-negative                                        |

**Response:** `200 OK` — Returns the full updated profile (same shape as GET).

**Errors:**

| Code                | Status | Condition                                    |
| ------------------- | ------ | -------------------------------------------- |
| `PROFILE_NOT_FOUND` | 404    | No profile exists for the authenticated user |
| `VALIDATION_ERROR`  | 400    | Request body fails validation                |
| `UNAUTHORIZED`      | 401    | Missing or invalid JWT                       |

---

## Resume Endpoints

### Upload Resume

Upload a PDF or DOCX resume file. Marks all previous resumes as not latest. Triggers asynchronous parsing via MassTransit (RabbitMQ).

```
POST /api/v1/profiles/me/resumes
```

**Authorization:** Any authenticated user

**Content-Type:** `multipart/form-data`

**Form Fields:**

| Field  | Type   | Required | Description                      |
| ------ | ------ | -------- | -------------------------------- |
| `file` | `file` | Yes      | Resume file (PDF or DOCX format) |

**Constraints:**

| Constraint    | Value           |
| ------------- | --------------- |
| Max file size | 25 MB           |
| Allowed types | `.pdf`, `.docx` |

**Response:** `201 Created`

```json
{
  "id": "770e8400-e29b-41d4-a716-446655440000",
  "user_id": "550e8400-e29b-41d4-a716-446655440000",
  "file_url": "/uploads/resumes/770e8400_resume.pdf",
  "original_filename": "john-doe-resume.pdf",
  "file_size_bytes": 245760,
  "file_type": "PDF",
  "is_latest": true,
  "is_parsed": false,
  "parse_error": null,
  "parsed_at": null,
  "created_at": "2026-04-01T12:00:00Z",
  "updated_at": "2026-04-01T12:00:00Z"
}
```

**Background Processing:**

After upload, a `ResumeUploadedEvent` is published to the message broker. The `ResumeUploadedConsumer` processes the event asynchronously:

1. **Basic parsing** — Extracts plain text and keyword-based skills from the file (PdfPig for PDF, OpenXml for DOCX).
2. **AI parsing** (optional) — Sends extracted text to the AI Service (`POST /api/v1/ai/resumes/parse`) for structured extraction (skills with levels/years, experience, education, certifications). Falls back gracefully if the AI Service is unavailable.
3. Updates `is_parsed`, `parsed_text`, `extracted_skills`, `ai_parsed_content`, and `parsed_at` on the resume record.

**Errors:**

| Code              | Status | Condition                                   |
| ----------------- | ------ | ------------------------------------------- |
| `INVALID_REQUEST` | 400    | Unsupported file type or file size exceeded |
| `UNAUTHORIZED`    | 401    | Missing or invalid JWT                      |

---

### List My Resumes

Retrieve all resumes uploaded by the authenticated user, ordered by most recent first.

```
GET /api/v1/profiles/me/resumes
```

**Authorization:** Any authenticated user

**Response:** `200 OK`

```json
[
  {
    "id": "770e8400-e29b-41d4-a716-446655440000",
    "user_id": "550e8400-e29b-41d4-a716-446655440000",
    "file_url": "/uploads/resumes/770e8400_resume.pdf",
    "original_filename": "john-doe-resume-v2.pdf",
    "file_size_bytes": 245760,
    "file_type": "PDF",
    "is_latest": true,
    "is_parsed": true,
    "parse_error": null,
    "parsed_at": "2026-04-01T12:01:00Z",
    "created_at": "2026-04-01T12:00:00Z",
    "updated_at": "2026-04-01T12:01:00Z"
  },
  {
    "id": "880e8400-e29b-41d4-a716-446655440000",
    "user_id": "550e8400-e29b-41d4-a716-446655440000",
    "file_url": "/uploads/resumes/880e8400_resume.docx",
    "original_filename": "john-doe-resume-v1.docx",
    "file_size_bytes": 180224,
    "file_type": "DOCX",
    "is_latest": false,
    "is_parsed": true,
    "parse_error": null,
    "parsed_at": "2026-03-15T10:30:00Z",
    "created_at": "2026-03-15T10:30:00Z",
    "updated_at": "2026-04-01T12:00:00Z"
  }
]
```

---

### Get Resume by ID

Retrieve a specific resume by ID, if it belongs to the authenticated user.

```
GET /api/v1/profiles/me/resumes/{id}
```

**Authorization:** Any authenticated user

**Path Parameters:**

| Parameter | Type   | Description     |
| --------- | ------ | --------------- |
| `id`      | `uuid` | The resume's ID |

**Response:** `200 OK` — Returns a single resume (same shape as items in the list endpoint).

**Errors:**

| Code               | Status | Condition                                            |
| ------------------ | ------ | ---------------------------------------------------- |
| `RESUME_NOT_FOUND` | 404    | Resume does not exist or belongs to a different user |
| `UNAUTHORIZED`     | 401    | Missing or invalid JWT                               |

---

## Data Models

### Profile Response

| Field                  | Type             | Nullable | Description                                        |
| ---------------------- | ---------------- | -------- | -------------------------------------------------- |
| `user_id`              | `uuid`           | No       | User ID (shared PK with `auth.users`)              |
| `first_name`           | `string`         | No       | Applicant's first name                             |
| `last_name`            | `string`         | No       | Applicant's last name                              |
| `phone`                | `string`         | Yes      | Contact phone number                               |
| `city`                 | `string`         | Yes      | City of residence                                  |
| `country`              | `string`         | Yes      | Country of residence                               |
| `skills`               | `SkillDto[]`     | Yes      | Self-reported skills                               |
| `social_links`         | `SocialLinksDto` | Yes      | Social media links                                 |
| `documents`            | `DocumentDto[]`  | Yes      | Uploaded documents (cover letters, certifications) |
| `profile_completed_at` | `datetime`       | Yes      | When profile met tenant completion requirements    |
| `created_at`           | `datetime`       | No       | Profile creation timestamp                         |
| `updated_at`           | `datetime`       | No       | Last modification timestamp                        |

### Profile Completion Evaluation

The `profile_completed_at` field is automatically evaluated on every profile create and update. Completion is determined by the tenant's `ProfileSettings` (configured via Admin module). The evaluation checks:

1. **Required profile fields** — configurable list (e.g., `Phone`, `Skills`). Defaults: `Phone`, `Skills`.
2. **Minimum skills count** — configurable threshold. Default: `3`.
3. **Required social links** — configurable list (e.g., `LinkedIn`). Default: `LinkedIn`.
4. **Required documents** — configurable list (e.g., `CoverLetter`). Default: `CoverLetter`.
5. **Resume required** — whether at least one uploaded resume is required. Default: `true`.

When all requirements are met, `profile_completed_at` is set to the current timestamp. If the profile later fails to meet requirements (e.g., after a settings change), the field is cleared to `null`.

A completed profile is a prerequisite for entering the candidate matching pool.

### SkillDto

| Field   | Type     | Nullable | Description                                                   |
| ------- | -------- | -------- | ------------------------------------------------------------- |
| `name`  | `string` | No       | Skill name (e.g., "C#", "PostgreSQL")                         |
| `level` | `string` | Yes      | Proficiency: `Beginner`, `Intermediate`, `Advanced`, `Expert` |
| `years` | `int`    | Yes      | Years of experience                                           |

### SocialLinksDto

| Field       | Type     | Nullable | Description            |
| ----------- | -------- | -------- | ---------------------- |
| `linked_in` | `string` | Yes      | LinkedIn profile URL   |
| `git_hub`   | `string` | Yes      | GitHub profile URL     |
| `portfolio` | `string` | Yes      | Personal portfolio URL |

### DocumentDto

| Field         | Type       | Nullable | Description                         |
| ------------- | ---------- | -------- | ----------------------------------- |
| `type`        | `string`   | No       | Document type (e.g., "CoverLetter") |
| `url`         | `string`   | No       | Storage URL                         |
| `filename`    | `string`   | No       | Original filename                   |
| `uploaded_at` | `datetime` | No       | Upload timestamp                    |

### Resume Response

| Field               | Type       | Nullable | Description                            |
| ------------------- | ---------- | -------- | -------------------------------------- |
| `id`                | `uuid`     | No       | Resume ID                              |
| `user_id`           | `uuid`     | No       | Owning user ID                         |
| `file_url`          | `string`   | No       | Storage URL                            |
| `original_filename` | `string`   | No       | Original filename from upload          |
| `file_size_bytes`   | `long`     | No       | File size in bytes                     |
| `file_type`         | `string`   | No       | `PDF` or `DOCX`                        |
| `is_latest`         | `bool`     | No       | Whether this is the most recent resume |
| `is_parsed`         | `bool`     | No       | Whether async parsing has completed    |
| `parse_error`       | `string`   | Yes      | Error message if parsing failed        |
| `parsed_at`         | `datetime` | Yes      | When parsing completed                 |
| `created_at`        | `datetime` | No       | Upload timestamp                       |
| `updated_at`        | `datetime` | No       | Last modification timestamp            |
