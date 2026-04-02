# Internals

Developer-facing documentation for the host application infrastructure — middleware, configuration, DI wiring, and launch profiles.

These docs describe _how the API host is assembled_, not the business logic inside individual modules.

## Contents

| Document                                           | Covers                                                                           |
| -------------------------------------------------- | -------------------------------------------------------------------------------- |
| [middleware.md](middleware.md)                     | Request pipeline, middleware ordering, and what each middleware does             |
| [configuration.md](configuration.md)               | `appsettings.json` sections, strongly-typed options, environment overrides       |
| [dependency-injection.md](dependency-injection.md) | `ModuleServiceCollectionExtensions`, module registration, cross-cutting services |
| [launch-settings.md](launch-settings.md)           | `launchSettings.json` profiles, URLs, environment variables                      |

## Where to find related docs

- API reference & endpoint docs → [`docs/api-reference/`](../api-reference/README.md)
- Coding conventions → [`docs/conventions/`](../conventions/CONTRIBUTING.md)
- Database designs → [`docs/database-designs/`](../database-designs/)
- Technical overview → [`docs/TECHNICAL_OVERVIEW.md`](../TECHNICAL_OVERVIEW.md)
