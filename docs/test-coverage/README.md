# Test Coverage

> Living document tracking all implemented tests, their coverage, rationale, and expected outcomes.

## Test Summary

> Counts are **runtime test cases** — `[Theory]` methods with `[InlineData]`/`[MemberData]` expand into multiple cases.

| Project                   | Tests    | Status                                    |
| ------------------------- | -------- | ----------------------------------------- |
| Jobsite.UnitTests         | 817      | ✅ All passing                            |
| Jobsite.ArchitectureTests | 78       | ✅ All passing                            |
| Jobsite.IntegrationTests  | 263      | ✅ All passing (all tests require Docker) |
| **Total**                 | **1158** |                                           |

---

## Module Coverage

> Counts are `[Fact]`/`[Theory]` test methods. Runtime test cases (1,107) are higher due to `[Theory]` data expansion.

| Module / Area        | Unit    | Integration | Total    | Doc                                                                   |
| -------------------- | ------- | ----------- | -------- | --------------------------------------------------------------------- |
| SharedKernel         | 46      | —           | 46       | [shared-kernel.md](shared-kernel.md)                                  |
| Tenancy              | 46      | 15          | 61       | [tenancy.md](tenancy.md)                                              |
| Auth                 | 94      | 27          | 121      | [auth.md](auth.md)                                                    |
| Admin                | 41      | 16          | 57       | [admin.md](admin.md)                                                  |
| Profiles             | 75      | 22          | 97       | [profiles.md](profiles.md)                                            |
| Recruitment          | 189     | 44          | 233      | [recruitment.md](recruitment.md)                                      |
| Screening            | 107     | 17          | 124      | [screening.md](screening.md)                                          |
| Matching             | 50      | 28          | 78       | [matching.md](matching.md)                                            |
| HR Workflows         | 36      | —           | 36       | [hr-workflows.md](hr-workflows.md)                                    |
| Middleware           | 30      | —           | 30       | [middleware.md](middleware.md)                                        |
| Pipeline Behaviors   | 7       | —           | 7        | [pipeline-behaviors.md](pipeline-behaviors.md)                        |
| Infrastructure       | 7       | —           | 7        | [infrastructure.md](infrastructure.md)                                |
| Contracts (WireMock) | —       | 24          | 24       | [integration.md](integration.md)                                      |
| E2E Pipelines        | —       | 38          | 38       | [integration.md](integration.md)                                      |
| Endpoint Tests (WAF) | —       | 32          | 32       | [integration.md](integration.md#endpoint-tests-webapplicationfactory) |
| Architecture         | —       | —           | 15       | [architecture.md](architecture.md)                                    |
| **Total**            | **728** | **263**     | **1006** |                                                                       |

---

## Coverage Gaps & Next Steps

### Profiles Module Gaps

| Area                         | Gap                                                                                  | Priority |
| ---------------------------- | ------------------------------------------------------------------------------------ | -------- |
| **Profile Endpoint Tests**   | No `WebApplicationFactory` HTTP pipeline tests for profile CRUD or resume endpoints. | Medium   |
| **MassTransit Consumer E2E** | No end-to-end test with Testcontainers RabbitMQ for resume upload → parse pipeline.  | Medium   |

### Recruitment Module Gaps

| Area                           | Gap                                                                                                               | Priority |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------- | -------- |
| **Recruitment Endpoint Tests** | No `WebApplicationFactory` tests for Recruitment endpoints (job posting CRUD, criteria, questions, applications). | Medium   |

### Cross-Module Gaps

| Area                           | Gap                                                                                                                                                                                                                                 | Priority |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| ~~**Endpoint Tests**~~         | ~~No `WebApplicationFactory` tests for `TenantEndpoints` or `AuthEndpoints`~~ ✅ 32 endpoint tests implemented (Health, Auth, Tenant, Tenant Isolation). See [integration.md](integration.md#endpoint-tests-webapplicationfactory). | ~~High~~ |
| ~~**Tenant Isolation Depth**~~ | ~~No cross-tenant data visibility tests~~ ✅ 6 tenant isolation tests implemented (inactive/deactivated/provisioning/non-existent tenant, cross-tenant token, cache verification).                                                  | ~~High~~ |
| ~~**Auth Endpoint Tests**~~    | ~~Service-level E2E exists (`AuthPipelineTests`) but no `WebApplicationFactory` HTTP endpoint tests~~ ✅ 15 auth endpoint tests implemented (register, login, refresh, logout, me, error envelope, full flow).                      | ~~High~~ |
| **Module Endpoint Tests**      | No `WebApplicationFactory` tests for Profiles, Recruitment, Screening, Matching, HR Workflows, Admin endpoints                                                                                                                      | Medium   |
| **Screening Endpoint Tests**   | No HTTP pipeline tests for `ScreeningEndpoints` (GET result, POST review, GET feedback, assessments)                                                                                                                                | Medium   |
| **MassTransit Integration**    | No end-to-end test with Testcontainers RabbitMQ — requires Testcontainers.RabbitMq package                                                                                                                                          | Medium   |
| **RequestLoggingMiddleware**   | Not directly tested — logs via Serilog, lower value without log sink assertions                                                                                                                                                     | Low      |

### Blocked by AI Service

All 6 AI client contract tests are now implemented using WireMock (see [screening.md](screening.md#ai-service-contract-tests-wiremock)). The full screening pipeline E2E test is also implemented (see [screening.md](screening.md#e2e-screening-pipeline-tests)). Remaining gaps:

| Area                               | Gap                                                                                         |
| ---------------------------------- | ------------------------------------------------------------------------------------------- |
| **Full Resume Parse Pipeline E2E** | End-to-end resume upload → basic parse → AI parse → persist requires operational AI Service |

### Blocked by Incomplete Modules

| Area                        | Gap                                                                                                                         |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| ~~**Matching Module**~~     | ~~Phase 7 — not yet implemented.~~ ✅ Implemented with 78 tests (50 unit + 28 integration). See [matching.md](matching.md). |
| ~~**HR Workflows Module**~~ | ~~Phase 8 — not yet implemented.~~ ✅ Implemented with 36 unit tests. See [hr-workflows.md](hr-workflows.md).               |
| **Admin Dashboard Stats**   | `GET /api/v1/admin/dashboard` deferred until pipeline modules provide aggregate data.                                       |
| **AI Interview Service**    | Deferred indefinitely — interview sessions, broker consumers, media transcription. See `docs/TODO.md`.                      |
