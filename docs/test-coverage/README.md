# Test Coverage

> Living document tracking all implemented tests, their coverage, rationale, and expected outcomes.

## Test Summary

| Project                   | Tests    | Status                                    |
| ------------------------- | -------- | ----------------------------------------- |
| Jobsite.UnitTests         | 798      | ✅ All passing                            |
| Jobsite.ArchitectureTests | 35       | ✅ All passing                            |
| Jobsite.IntegrationTests  | 181      | ✅ All passing (all tests require Docker) |
| **Total**                 | **1014** |                                           |

---

## Module Coverage

| Module / Area      | Tests | Doc                                            |
| ------------------ | ----- | ---------------------------------------------- |
| SharedKernel       | 27    | [shared-kernel.md](shared-kernel.md)           |
| Tenancy            | 25    | [tenancy.md](tenancy.md)                       |
| Auth               | 89    | [auth.md](auth.md)                             |
| Admin              | 39    | [admin.md](admin.md)                           |
| Profiles           | 105   | [profiles.md](profiles.md)                     |
| Recruitment        | 189   | [recruitment.md](recruitment.md)               |
| Screening          | 178   | [screening.md](screening.md)                   |
| Matching           | 79    | [matching.md](matching.md)                     |
| HR Workflows       | 36    | [hr-workflows.md](hr-workflows.md)             |
| Middleware         | 21    | [middleware.md](middleware.md)                 |
| Pipeline Behaviors | 7     | [pipeline-behaviors.md](pipeline-behaviors.md) |
| Infrastructure     | 2     | [infrastructure.md](infrastructure.md)         |
| Architecture Tests | 35    | [architecture.md](architecture.md)             |
| Integration Tests  | 181   | [integration.md](integration.md)               |

---

## Coverage Gaps & Next Steps

### Profiles Module Gaps

| Area                         | Gap                                                                                  | Priority |
| ---------------------------- | ------------------------------------------------------------------------------------ | -------- |
| **Profile Endpoint Tests**   | No `WebApplicationFactory` HTTP pipeline tests for profile CRUD or resume endpoints. | Medium   |
| **MassTransit Consumer E2E** | No end-to-end test with Testcontainers RabbitMQ for resume upload → parse pipeline.  | Medium   |

### Recruitment Module (Phase 4) Gaps

| Area                             | Gap                                                                                                                                                                                      | Priority |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| **Recruitment Repository Tests** | No repository integration tests for `IJobPostingRepository`, `IApplicationRepository`, `IClientCompanyRepository` — only DbContext-level tests exist (22 in `RecruitmentDbContextTests`) | High     |
| **Recruitment Endpoint Tests**   | No `WebApplicationFactory` tests for Recruitment endpoints (job posting CRUD, criteria, questions, applications)                                                                         | Medium   |

### Cross-Module Gaps

| Area                         | Gap                                                                                                  | Priority |
| ---------------------------- | ---------------------------------------------------------------------------------------------------- | -------- |
| **Endpoint Tests**           | No `WebApplicationFactory` tests for `TenantEndpoints` or `AuthEndpoints` — needs full HTTP pipeline | High     |
| **Tenant Isolation Depth**   | No cross-tenant data visibility tests (write via tenant A, query via tenant B → zero results)        | High     |
| **Auth Flow E2E**            | No end-to-end register → login → refresh → logout integration test through HTTP endpoints            | High     |
| **Screening Endpoint Tests** | No HTTP pipeline tests for `ScreeningEndpoints` (GET result, POST review, GET feedback, assessments) | Medium   |
| **MassTransit Integration**  | No end-to-end test with Testcontainers RabbitMQ — requires Testcontainers.RabbitMq package           | Medium   |
| **RequestLoggingMiddleware** | Not directly tested — logs via Serilog, lower value without log sink assertions                      | Low      |

### Blocked by AI Service

All 6 AI client contract tests are now implemented using WireMock (see [screening.md](screening.md#ai-service-contract-tests-wiremock)). The full screening pipeline E2E test is also implemented (see [screening.md](screening.md#e2e-screening-pipeline-tests)). Remaining gaps:

| Area                               | Gap                                                                                        |
| ---------------------------------- | ------------------------------------------------------------------------------------------ |
| **Full Resume Parse Pipeline E2E** | End-to-end resume upload → basic parse → AI parse → persist requires operational AI Service |

### Blocked by Incomplete Modules

| Area                      | Gap                                                                                                            |
| ------------------------- | -------------------------------------------------------------------------------------------------------------- |
| ~~**Matching Module**~~   | ~~Phase 7 — not yet implemented.~~ ✅ Implemented with 39 unit tests. See [matching.md](matching.md).          |
| **HR Workflows Module**   | Phase 8 — not yet implemented. No entities, services, or tests exist.                                          |
| **Admin Dashboard Stats** | `GET /api/v1/admin/dashboard` deferred until pipeline modules (Matching, HR Workflows) provide aggregate data. |
| **AI Interview Service**  | Deferred indefinitely — interview sessions, broker consumers, media transcription. See `docs/TODO.md`.         |
