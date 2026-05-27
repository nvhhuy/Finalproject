# Backend docs hub

Table of contents for `src/Backend/` (Api, Application, Domains, Infrastructure, Worker, Observability, Tests).

**Cursor rule:** `.cursor/rules/backend.mdc` — agents and developers working under `src/Backend/**` should follow it.

---

## By goal

### 1) New developer — run locally in ~30 minutes

1. [`ONBOARDING.md`](./ONBOARDING.md)
2. [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md)
3. [`ARCHITECTURE.md`](./ARCHITECTURE.md) + [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md)

### 2) What exists today (endpoints, modules)

1. [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) — full HTTP catalog
2. [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md) — short status matrix
3. [`CODEBASE_INCLUDED_AND_USAGE_GUIDE.md`](./CODEBASE_INCLUDED_AND_USAGE_GUIDE.md)

### 3) Add or change a backend feature

1. [`BASE_GUIDE.md`](./BASE_GUIDE.md)
2. [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md)
3. [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
4. [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md)

### 4) Database / CI

1. [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md)
2. [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md)

### 5) Production / hardening

1. [`PRODUCTION_READINESS_REPORT.md`](./PRODUCTION_READINESS_REPORT.md)
2. [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md)

---

## By topic (complete index)

| Topic | Document |
|-------|----------|
| **Architecture & DDD boundaries** | [`ARCHITECTURE.md`](./ARCHITECTURE.md) |
| **Solution / folder map** | [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md) |
| **Reference coding style** | [`STYLE_FROM_REFERENCE_PROJECTS.md`](./STYLE_FROM_REFERENCE_PROJECTS.md) |
| **Transactions, events, repositories** | [`BASE_GUIDE.md`](./BASE_GUIDE.md) |
| **Auth, cache, outbox, worker, admin ops** | [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) |
| **Api ↔ Worker, RabbitMQ, outbox** | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) |
| **DataProtection key ring** | [`DATA_PROTECTION.md`](./DATA_PROTECTION.md) |
| **EF LINQ translation pitfalls** | [`EF_PITFALLS.md`](./EF_PITFALLS.md) |
| **Caching (read-through vs ephemeral)** | [`CACHE_GUIDE.md`](./CACHE_GUIDE.md) |
| **Optimistic concurrency (`rowVersion`)** | [`OPTIMISTIC_CONCURRENCY.md`](./OPTIMISTIC_CONCURRENCY.md) |
| **Feature inventory (HTTP)** | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| **Baseline matrix** | [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md) |
| **Included packages & toggles** | [`CODEBASE_INCLUDED_AND_USAGE_GUIDE.md`](./CODEBASE_INCLUDED_AND_USAGE_GUIDE.md) |
| **Migrations** | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) |
| **Gitea CI** | [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) |
| **Strict AI implementation rules** | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) |

---

## Solution map (quick reference)

| Project | Role |
|---------|------|
| **Api** | HTTP, JWT, Swagger, filters, `Api/Models` |
| **Application** | MediatR commands/queries, validators, Mapperly |
| **Domains** | Aggregates, value objects, domain events |
| **Infrastructure** | EF, auth, cache, messaging, outbox, scheduling |
| **Worker** | Outbox publisher, Rabbit consumers, cron jobs |
| **Observability** | Serilog bootstrap, OpenTelemetry |
| **Shared** | `Result<T,E>`, typed errors |
| **Tests/** | Application, Api.Integration, Infrastructure, Domains, Worker |

**Registration**

- Api: `AddWebApi` → `AddApplication` → `AddInfrastructure` (includes JWT).
- Worker: `AddApplication` → `AddInfrastructureCore` → `AddOutboxPublisher` → `AddWorkerScheduling`.

**Local infra:** `local/docker-compose.yml` (Postgres, Redis, RabbitMQ, MinIO, …). See repo [`local/README.md`](../../local/README.md).

**Run**

```bash
dotnet run --project src/Backend/Api
dotnet run --project src/Backend/Worker
dotnet test src/Backend/Backend.sln
```

**Dev admin seed** (when `Seed:Admin:Enabled` in Development): `admin@localhost.com` / `Temp!ChangeMe123`.

---

## Maintenance rule

When backend behavior changes:

| Change | Update |
|--------|--------|
| New/changed HTTP endpoint | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Onboarding / ports / seed | [`ONBOARDING.md`](./ONBOARDING.md) |
| Messaging / worker / outbox | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) |
| EF query patterns / pitfalls | [`EF_PITFALLS.md`](./EF_PITFALLS.md) |
| DataProtection / 2FA deploy | [`DATA_PROTECTION.md`](./DATA_PROTECTION.md) |
| Schema | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) + EF migration |
| CI | [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) |
| Concurrency contract | [`OPTIMISTIC_CONCURRENCY.md`](./OPTIMISTIC_CONCURRENCY.md) |

Also keep [`.cursor/rules/backend.mdc`](../../.cursor/rules/backend.mdc) aligned if conventions change.
