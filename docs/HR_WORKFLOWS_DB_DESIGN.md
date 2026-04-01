# HR Workflows Module — Database Design

The human side of hiring — final interviews and job offers. This is where automated pipeline results meet human judgment. After the Matching module shortlists candidates, HR Workflows takes over with in-person (or video) interviews conducted by real people, followed by offer management.

Listens for `CandidateShortlistedEvent` from Matching. Publishes `FinalInterviewScheduledEvent` and `OfferExtendedEvent`.

---

## Tables

### final_interviews

A scheduled final interview for a shortlisted candidate. This is the human interview — distinct from the AI Interview Service's automated digital interview earlier in the pipeline. Each final interview can have multiple panelists (interviewers) who each provide independent feedback.

One-to-one with `recruitment.applications` using a shared primary key. An application can have at most one final interview.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| application_id | uuid | PK, FK → recruitment.applications.id | Shared key — one final interview per application |
| status | varchar(20) | NOT NULL | Enum: `Scheduled`, `InProgress`, `Completed`, `Cancelled`, `NoShow` |
| interview_type | varchar(20) | NOT NULL | Enum: `InPerson`, `Video`, `Phone` |
| scheduled_at | timestamp | NOT NULL | When the interview is scheduled to take place |
| duration_minutes | integer | NOT NULL, DEFAULT 60 | Expected interview duration |
| location | varchar(500) | nullable | Physical address for `InPerson`, video call URL for `Video`, phone number for `Phone` |
| scheduled_by | uuid | NOT NULL, FK → auth.users.id | The recruiter or hiring manager who scheduled this interview |
| overall_recommendation | varchar(20) | nullable | Enum: `StrongHire`, `Hire`, `NoHire`, `StrongNoHire`. Aggregated from panelist recommendations. Set by hiring manager after reviewing all feedback |
| decision_notes | text | nullable | Hiring manager's summary of the interview outcome and reasoning for the recommendation |
| decided_by | uuid | nullable, FK → auth.users.id | The hiring manager who made the final recommendation |
| decided_at | timestamp | nullable | When the final recommendation was recorded |
| completed_at | timestamp | nullable | When the interview actually finished |
| cancelled_at | timestamp | nullable | When the interview was cancelled. Set when status → `Cancelled` |
| cancellation_reason | varchar(500) | nullable | Why it was cancelled (e.g., "Candidate requested reschedule", "Position filled") |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_final_interviews_status | status | Non-unique | Filter by interview status (upcoming, completed, etc.) |
| ix_final_interviews_scheduled_at | scheduled_at | Non-unique | Calendar views, upcoming interview queries |
| ix_final_interviews_scheduled_by | scheduled_by | Non-unique | "Interviews I scheduled" for recruiters |
| ix_final_interviews_recommendation | overall_recommendation | Non-unique | Filter by recommendation for hiring dashboards |

---

### interview_panelists

Individual interviewers assigned to a final interview. Each panelist provides their own independent rating, notes, and hire/no-hire recommendation. A panel can be one person (simple interview) or several (panel interview).

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| id | uuid | PK | |
| interview_id | uuid | NOT NULL, FK → final_interviews.application_id | The interview this panelist is assigned to |
| interviewer_id | uuid | NOT NULL, FK → auth.users.id | The staff member conducting the interview. Typically has `Interviewer`, `HiringManager`, or `Recruiter` role |
| rating | decimal(3,1) | nullable | Panelist's overall rating (1.0–5.0). NULL until feedback is submitted |
| recommendation | varchar(20) | nullable | Enum: `StrongHire`, `Hire`, `NoHire`, `StrongNoHire`. Panelist's individual recommendation. NULL until feedback is submitted |
| strengths | text | nullable | What the candidate did well in this interviewer's assessment |
| concerns | text | nullable | Areas of weakness or concern |
| notes | text | nullable | General interview notes, observations, questions asked |
| feedback_submitted_at | timestamp | nullable | When this panelist submitted their feedback. NULL = hasn't submitted yet |
| created_at | timestamp | NOT NULL | |

**Constraints:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| uq_panelists_interview_interviewer | interview_id, interviewer_id | Unique | One feedback entry per interviewer per interview |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_panelists_interview_id | interview_id | Non-unique | "All panelists for this interview" |
| ix_panelists_interviewer_id | interviewer_id | Non-unique | "All interviews this person is assigned to" — interviewer's dashboard |
| ix_panelists_feedback_pending | interview_id, feedback_submitted_at | Non-unique | Find panelists who haven't submitted feedback yet |

---

### job_offers

A formal job offer extended to a candidate. Created after a positive final interview recommendation. Tracks the offer terms and the candidate's response.

One-to-one with `recruitment.applications` using a shared primary key. An application can have at most one active offer.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| application_id | uuid | PK, FK → recruitment.applications.id | Shared key — one offer per application |
| client_company_id | uuid | nullable, FK → recruitment.client_companies.id | The client company the offer is on behalf of. Denormalized from the job posting for offer letter generation. NULL if the tenant is hiring for themselves |
| status | varchar(20) | NOT NULL | Enum: `Draft`, `Pending`, `Accepted`, `Declined`, `Withdrawn`, `Expired` |
| salary | decimal(12,2) | NOT NULL | Offered salary amount |
| salary_currency | varchar(3) | NOT NULL | ISO 4217 currency code (e.g., `USD`, `EUR`) |
| salary_period | varchar(20) | NOT NULL | Enum: `Annual`, `Monthly`, `Hourly`. Pay period for the salary figure |
| employment_type | varchar(20) | NOT NULL | Enum: `FullTime`, `PartTime`, `Contract`, `Temporary`. Should match or be compatible with the job posting's employment type |
| start_date | date | nullable | Proposed start date |
| benefits | text | nullable | Description of benefits package (health, PTO, equity, etc.) |
| additional_terms | text | nullable | Any other terms or conditions (relocation assistance, signing bonus, etc.) |
| offer_letter_url | varchar(2048) | nullable | CDN/blob storage URL to the formal offer letter document |
| expires_at | timestamp | nullable | Offer expiration deadline. Status moves to `Expired` after this |
| extended_by | uuid | NOT NULL, FK → auth.users.id | The hiring manager or recruiter who extended the offer |
| extended_at | timestamp | nullable | When the offer was sent to the candidate. Set when status moves from `Draft` to `Pending` |
| responded_at | timestamp | nullable | When the candidate accepted or declined |
| decline_reason | varchar(500) | nullable | If declined: why the candidate turned it down (free-text, optional) |
| withdrawn_at | timestamp | nullable | If the company withdrew the offer before the candidate responded |
| withdrawal_reason | varchar(500) | nullable | Why the offer was withdrawn |
| created_at | timestamp | NOT NULL | |
| updated_at | timestamp | NOT NULL | Auto-set on modification |

**Indexes:**

| Name | Columns | Type | Purpose |
|------|---------|------|---------|
| ix_job_offers_status | status | Non-unique | Filter by offer status for dashboards |
| ix_job_offers_extended_by | extended_by | Non-unique | "Offers I extended" for hiring managers |
| ix_job_offers_expires_at | expires_at | Non-unique | Background job to expire stale offers |

---

## Schema

```sql
CREATE SCHEMA IF NOT EXISTS hr_workflows;
```

All HR Workflows module tables live under the `hr_workflows` schema.

---

## Relationships

```
recruitment.applications      ||--o| final_interviews : "has (optional, one-to-one)"
final_interviews              ||--o{ interview_panelists : "has many (interview panel)"
recruitment.applications      ||--o| job_offers : "has (optional, one-to-one)"
recruitment.client_companies  ||--o{ job_offers : "offer on behalf of client (optional)"
auth.users                    ||--o{ final_interviews : "scheduled_by / decided_by"
auth.users                    ||--o{ interview_panelists : "interviewer_id"
auth.users                    ||--o{ job_offers : "extended_by"
```

---

## Final Interview Status Lifecycle

```
Scheduled → InProgress → Completed
         → Cancelled
         → NoShow
```

- **Scheduled**: Interview is on the calendar. Panelists assigned. `FinalInterviewScheduledEvent` published. Application status → `FinalInterview`.
- **InProgress**: Interview is happening now. Optional — can skip directly to `Completed` if real-time tracking isn't needed.
- **Completed**: Interview finished. Panelists submit feedback. Hiring manager records `overall_recommendation`. `completed_at` set.
- **Cancelled**: Interview was cancelled before it took place. `cancelled_at` and `cancellation_reason` set. Application may be rescheduled (new interview) or rejected.
- **NoShow**: Candidate didn't show up. Distinct from cancellation — the interview was expected to happen.

---

## Interview Feedback Flow

```
1. CandidateShortlistedEvent received
2. Recruiter/hiring manager schedules final interview
   → final_interviews row created (status = Scheduled)
   → Panelists assigned (interview_panelists rows created)
   → FinalInterviewScheduledEvent published
   → Application status → FinalInterview
3. Interview takes place
   → Status → InProgress (optional) → Completed
4. Each panelist submits feedback independently:
   → rating, recommendation, strengths, concerns, notes
   → feedback_submitted_at set
5. Hiring manager reviews all panelist feedback
6. Hiring manager records overall_recommendation and decision_notes
   → decided_by, decided_at set
7. Based on recommendation:
   → StrongHire / Hire → proceed to job offer
   → NoHire / StrongNoHire → Application status → Rejected
      (rejected_at_stage = FinalInterview)
```

**Panelist feedback is independent.** Each interviewer submits their own assessment without seeing others' feedback first. This prevents anchoring bias. The hiring manager sees all feedback together when making the final recommendation.

---

## Job Offer Status Lifecycle

```
Draft → Pending → Accepted
                → Declined
                → Expired
                → Withdrawn
```

- **Draft**: Offer terms being prepared. Not yet visible to the candidate.
- **Pending**: Offer sent to the candidate. `extended_at` set. `OfferExtendedEvent` published. Application status → `Offered`. Clock starts on `expires_at` if set.
- **Accepted**: Candidate accepted. `responded_at` set. Application status → `Hired`. Terminal success state.
- **Declined**: Candidate turned it down. `responded_at` and optionally `decline_reason` set. Application status → `Rejected` (rejected_at_stage = Offered).
- **Expired**: Offer deadline passed without a response. Background job sets this when `expires_at < now()` and status is still `Pending`.
- **Withdrawn**: Company pulled the offer before the candidate responded. `withdrawn_at` and `withdrawal_reason` set. Distinct from expiration — this is an active decision by the company.

---

## Offer Flow

```
1. Positive final interview recommendation (StrongHire or Hire)
2. Hiring manager creates job offer
   → job_offers row created (status = Draft)
   → Salary, terms, start date filled in
   → Optional: offer letter document uploaded
3. Offer reviewed and approved internally
4. Offer extended to candidate
   → Status → Pending, extended_at set
   → OfferExtendedEvent published
   → Application status → Offered
5. Candidate responds:
   a. Accepts → status → Accepted, responded_at set
      → Application status → Hired
   b. Declines → status → Declined, responded_at set, decline_reason captured
      → Application status → Rejected (rejected_at_stage = Offered)
6. If no response by expires_at:
   → Background job sets status → Expired
   → Application status stays at Offered (recruiter follows up manually)
```

---

## Design Decisions

**Shared primary key for `final_interviews` and `job_offers`.** One final interview per application, one offer per application — enforced at the database level. If a first interview goes badly and they want to re-interview, the current record should be cancelled and a new one scheduled. Same for offers — withdraw the first, create a new one. For simplicity the PK constraint enforces one active record; if re-interview/re-offer flows become common, this can be relaxed to a regular FK with a unique constraint on `(application_id, status)` or similar.

**Separate `interview_panelists` table, not a single `interviewer_id`.** Panel interviews are standard practice, especially for senior roles. Multiple interviewers each bring different perspectives (technical, cultural, domain). A join table with independent feedback per panelist is the only design that supports this properly. Single-interviewer interviews are just panels of one — the same schema works for both.

**Independent panelist feedback.** Each interviewer submits their own rating, recommendation, strengths, concerns, and notes without seeing others' submissions first. This prevents groupthink and anchoring bias. The hiring manager aggregates the feedback into an `overall_recommendation` on the interview record.

**Four-level recommendation scale.** `StrongHire`, `Hire`, `NoHire`, `StrongNoHire` — not a numeric score. Interview recommendations are inherently qualitative judgments. A 4-point scale forces a directional decision (no neutral middle option) while allowing for conviction levels. This is common practice at many tech companies.

**`overall_recommendation` set by hiring manager, not computed.** The hiring manager reads all panelist feedback and makes a judgment call. The system doesn't auto-compute an average — three `Hire` votes and one `StrongNoHire` might mean different things depending on who gave the `StrongNoHire` and why. This is a human decision.

**Offer terms as flat columns, not JSONB.** Salary, currency, period, employment type, start date — these are well-defined, frequently queried fields. Unlike skills or social links (which grow unpredictably), offer terms are a stable set of fields. Flat columns are clearer, indexable, and easier to validate than a JSONB blob.

**`Draft` status on offers.** Offers often need internal approval before being sent to the candidate. Draft → Pending is that gate. The `extended_at` timestamp distinguishes "when was it created" from "when was it actually sent."

**`Expired` and `Withdrawn` as distinct terminal states.** Expiration is passive (time ran out). Withdrawal is active (company pulled the offer). The distinction matters for reporting (high withdrawal rate = internal decision issues; high expiration rate = candidates ghosting or offers not compelling) and for candidate communication (different messages for each).

**`decline_reason` and `withdrawal_reason` as free-text.** These are infrequent, qualitative data points. Structured enums would be too rigid — there are too many possible reasons, and the value is in the detail, not the categorization. If trend analysis is needed later, a classification can be applied in the application layer.

**No negotiation history table.** The current design stores the final offer terms, not a back-and-forth. If the candidate counters, the recruiter updates the offer terms (salary, start date, etc.) while it's still in `Draft` or re-creates the offer. A full `offer_negotiations` table with rounds and counter-offers is premature — add it if negotiation tracking becomes a real requirement.

**`client_company_id` denormalized on job offers.** The client company is available via `application → job_posting → client_company`, but the offer letter needs the employer name directly — it's the company making the offer, not the agency. Denormalizing here makes offer letter generation self-contained without traversing the full FK chain. NULL for non-agency tenants.

**Cross-schema FKs to `recruitment.applications`, `recruitment.client_companies`, and `auth.users`.** HR Workflows is the final stretch of the pipeline that started in Recruitment. The FKs make the dependency chain explicit: application → final interview → offer → hired.

**No `updated_at` on `interview_panelists`.** Panelist feedback is submitted once. After `feedback_submitted_at` is set, the record is effectively immutable. If feedback needs correction, the panelist can re-submit (update the row), but that's rare enough to not warrant a general change-tracking timestamp.
