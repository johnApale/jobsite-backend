# Architecture Tests Coverage

← [Test Coverage](README.md)

> Architecture tests enforce structural rules at build time using NetArchTest. They prevent architectural drift as the codebase grows.

---

## `LayerDependencyTests` (15 tests)

Enforces the module layer dependency direction: `Domain → SharedKernel only`, `Application → Domain only`. Covers Tenancy, Profiles, and Recruitment modules.

| Test                                                            | What It Verifies                                                             | Expected Outcome         |
| --------------------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------ |
| `DomainLayer_ShouldNotReference_ApplicationLayer`               | Tenancy.Domain has no dependency on Tenancy.Application                      | No violating types found |
| `DomainLayer_ShouldNotReference_InfrastructureLayer`            | Tenancy.Domain has no dependency on Tenancy.Infrastructure                   | No violating types found |
| `DomainLayer_ShouldNotReference_EFCore`                         | Tenancy.Domain has no dependency on `Microsoft.EntityFrameworkCore`          | No violating types found |
| `ApplicationLayer_ShouldNotReference_InfrastructureLayer`       | Tenancy.Application has no dependency on Tenancy.Infrastructure              | No violating types found |
| `ApplicationLayer_ShouldNotReference_EFCore`                    | Tenancy.Application has no dependency on `Microsoft.EntityFrameworkCore`     | No violating types found |
| `ProfilesDomain_ShouldNotReference_ApplicationLayer`            | Profiles.Domain has no dependency on Profiles.Application                    | No violating types found |
| `ProfilesDomain_ShouldNotReference_InfrastructureLayer`         | Profiles.Domain has no dependency on Profiles.Infrastructure                 | No violating types found |
| `ProfilesDomain_ShouldNotReference_EFCore`                      | Profiles.Domain has no dependency on `Microsoft.EntityFrameworkCore`         | No violating types found |
| `ProfilesApplication_ShouldNotReference_InfrastructureLayer`    | Profiles.Application has no dependency on Profiles.Infrastructure            | No violating types found |
| `ProfilesApplication_ShouldNotReference_EFCore`                 | Profiles.Application has no dependency on `Microsoft.EntityFrameworkCore`    | No violating types found |
| `RecruitmentDomain_ShouldNotReference_ApplicationLayer`         | Recruitment.Domain has no dependency on Recruitment.Application              | No violating types found |
| `RecruitmentDomain_ShouldNotReference_InfrastructureLayer`      | Recruitment.Domain has no dependency on Recruitment.Infrastructure           | No violating types found |
| `RecruitmentDomain_ShouldNotReference_EFCore`                   | Recruitment.Domain has no dependency on `Microsoft.EntityFrameworkCore`      | No violating types found |
| `RecruitmentApplication_ShouldNotReference_InfrastructureLayer` | Recruitment.Application has no dependency on Recruitment.Infrastructure      | No violating types found |
| `RecruitmentApplication_ShouldNotReference_EFCore`              | Recruitment.Application has no dependency on `Microsoft.EntityFrameworkCore` | No violating types found |

**Why:** The modular monolith architecture requires strict dependency direction to keep modules independently testable and refactorable. If the domain layer references EF Core, it becomes impossible to test business logic without a database. If application references infrastructure, swapping implementations (e.g., switching from PostgreSQL to another store) requires touching business logic. These tests catch accidental `using` statements added by IDE auto-imports.

---

## `NamingConventionTests` (4 tests)

Enforces coding standards from `docs/conventions/DOTNET_CONVENTIONS.md` across all modules and SharedKernel.

| Test                                                                                    | What It Verifies                                                                    | Expected Outcome         |
| --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- | ------------------------ |
| `ConcreteClasses_ShouldBeSealed_InDomain(module)` [Theory × all modules + SharedKernel] | All concrete classes in Domain layers are `sealed` (excluding EF migration classes) | No violating types found |
| `ConcreteClasses_ShouldBeSealed_InInfrastructure(module)` [Theory × all modules]        | All concrete classes in Infrastructure layers are `sealed`                          | No violating types found |
| `Interfaces_ShouldStartWithI_InDomain(module)` [Theory × all modules + SharedKernel]    | All interfaces follow the `I` prefix convention                                     | No violating types found |
| `Interfaces_ShouldStartWithI_InInfrastructure(module)` [Theory × all modules]           | All interfaces in Infrastructure follow the `I` prefix convention                   | No violating types found |

**Why:** The project mandate is `sealed class` on all concrete classes unless inheritance is explicitly needed. Applies across all 8 modules and SharedKernel, not just Tenancy. EF Core migration classes are excluded because they're auto-generated and inherit from `Migration`.

---

## `ModuleIsolationTests` (16 tests)

Enforces that modules do not cross-reference each other — modules communicate only through SharedKernel domain events. Tests all 8 modules via `[Theory]` with `[MemberData]`.

| Test                                                                | What It Verifies                                                               | Expected Outcome         |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------ | ------------------------ |
| `DomainLayer_ShouldNotReference_OtherModules(module)` [× 8]         | Each module's Domain has no dependency on any other module's namespace         | No violating types found |
| `InfrastructureLayer_ShouldNotReference_OtherModules(module)` [× 8] | Each module's Infrastructure has no dependency on any other module's namespace | No violating types found |

Modules tested: Tenancy, Auth, Admin, Profiles, Recruitment, Screening, Matching, HRWorkflows.

**Why:** Cross-module references are the primary way a modular monolith degrades into a big ball of mud. If Tenancy references Recruitment directly, extracting either into a separate service later becomes impossible. These tests enforce that inter-module communication goes through SharedKernel events only, keeping module boundaries clean.
