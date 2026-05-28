# Backend onboarding (new developers)

This guide helps new developers **get the project running in ~30 minutes** and know **what to read next**. This base is **production-ready** — fork it and **strip out core features** when needed, rather than rewriting auth/media/messaging from scratch.

---

## Where am I in the repo?

```
BaseProject/
├── src/Backend/          ← API, Application, Domains, Infrastructure, Worker
├── src/Frontend/         ← (if present) SPA client
├── local/docker-compose.yml
├── tools/backend/        ← migrate.sh, migration scripts, git hooks
├── docs/backend/         ← documentation you are reading
└── .github/workflows/    ← CI (migration drift check)
```

---

## Recommended reading path

| Order | Document | Time | Goal |
|-------|----------|------|------|
| 1 | **This file** (`ONBOARDING.md`) | 15 min | Run locally + understand the big picture |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 20 min | Know what the base already includes |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) + [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md) | 30 min | Layers, folders, bounded contexts |
| 4 | [`BASE_GUIDE.md`](./BASE_GUIDE.md) | 45 min | Flow for adding a specific feature |
| 5 | [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) | As needed | Auth, cache, logging in detail |
| 6 | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) | When enabling media/outbox | Api **does not** call Worker over HTTP |
| 7 | [`DATA_PROTECTION.md`](./DATA_PROTECTION.md) | When deploying / 2FA | Key ring `local/dataprotection-keys` (gitignored) |
| 8 | [`EF_PITFALLS.md`](./EF_PITFALLS.md) | When LINQ returns 500 | `Id.Value`, `DateOnly` GroupBy, … |
| 9 | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) | Before changing DB schema | Multi-person teams |
| 10 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) + [`.cursor/rules/backend.mdc`](../../.cursor/rules/backend.mdc) | When coding with AI | Mandatory rules |

**You do not need to read everything at once.** Day 1: items 1–3; week 1: add 4–6.

---

## Day 1 — checklist

### 1. Install prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Postgres, Redis, RabbitMQ, MinIO, …)
- `dotnet tool install --global dotnet-ef` (EF CLI)
- Git hooks (one time): `./tools/backend/git-hooks/install.sh`

### 2. Local infrastructure

From **repo root**:

```bash
docker compose -f local/docker-compose.yml up -d
```

Verify: Postgres `5432`, Redis `6379`, RabbitMQ `5672` / UI `15672`, MinIO `9000`.

### 3. Configure the API

Sample file: `src/Backend/Api/appsettings.Development.json`

- Connection string points to `localhost:5432`, database `baseproject`
- JWT secret is dev-only (must change in production)
- `Database:ApplyPendingMigrations: true` — API auto-migrates on start (dev)

### 4. Run API + Worker

```bash
# Terminal 1 — API
dotnet run --project src/Backend/Api

# Terminal 2 — Worker (media, outbox, cron)
dotnet run --project src/Backend/Worker
```

### 5. DataProtection keys (2FA)

The `local/dataprotection-keys/` directory is **not** in Git. Worker Dev already points `DataProtection:KeysDirectory` there; the first run creates the XML file automatically.

Also add the same `KeysDirectory` for **Api** Development (see [`DATA_PROTECTION.md`](./DATA_PROTECTION.md)) so 2FA secrets in the DB do not break after restarting Api.

### 6. Quick smoke test

- Swagger: `https://localhost:7124/swagger` (or the port in the log)
- Health: `GET /health/ready`
- Admin dev: `admin@localhost.com` / `Temp!ChangeMe123` (when seed is enabled)
- Register / login — see [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) Auth section

### 7. Tests

```bash
cd src/Backend
dotnet test --filter "FullyQualifiedName!~Api.IntegrationTests"
```

Integration tests require Postgres running:

```bash
dotnet test Tests/Api.IntegrationTests
```

---

## Architecture in one sentence

```
HTTP Request
    → Api (Controller)
    → MediatR (Command/Query + Validation + AdminAuth behaviors)
    → Application Handler
    → Domain (rules) + Infrastructure (EF, cache, email, storage, …)
    → IUnitOfWork.SaveChangesAsync (once per request when [Transactional])
```

**Golden rules:**

- Controllers stay **thin** — no business logic.
- Write to DB via **write repository** + `ApplicationDbContext`.
- Read via **read repository** / `IApplicationReadDbContext` (no-tracking).
- Important mutations: `[Transactional]` on the action.

---

## I want to… → Where to go

| Goal | Start here |
|------|------------|
| See what APIs exist | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Add a new API endpoint | [`BASE_GUIDE.md`](./BASE_GUIDE.md) § adding a feature |
| Change a DB table | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) + `migration/new.sh` |
| Debug auth / JWT | [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) § Authentication |
| Api ↔ Worker / queue | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) |
| DataProtection keys | [`DATA_PROTECTION.md`](./DATA_PROTECTION.md) |
| EF LINQ 500 errors | [`EF_PITFALLS.md`](./EF_PITFALLS.md) |
| Enable RabbitMQ / outbox | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) § Enable locally |
| Upload images/video | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) § Media + Worker |
| Set up CI on Gitea | [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) |
| Know what's still missing for prod | [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md) |

---

## Adding a new feature (7-step summary)

1. **Domain** — entity/aggregate in `Domains/Modules/<Feature>/`
2. **Application** — command/query + handler + validator in `Application/Modules/<Feature>/`
3. **Contracts** — repository/service interfaces in `Application/Contracts/`
4. **Infrastructure** — EF config, repository in `Infrastructure/Modules/<Feature>/`
5. **Migration** — `./tools/backend/migration/new.sh AddMyFeature`
6. **Api** — controller + models in `Api/Controllers`, `Api/Models`
7. **Test** — handler test in `Application.Tests`

Details + examples: [`BASE_GUIDE.md`](./BASE_GUIDE.md).

---

## Command cheat sheet

```bash
# Infra
docker compose -f local/docker-compose.yml up -d

# DB migrate
./tools/backend/migrate.sh

# Create migration
./tools/backend/migration/new.sh AddSomething

# Check migration before commit
./tools/backend/migration/check.sh --fast

# Build + test
dotnet build src/Backend
dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"
```

---

## Project roles (quick reference)

| Project | Edit when… |
|---------|------------|
| `Api` | Adding routes, middleware, auth policies |
| `Application` | Use-case logic, validation |
| `Domains` | Business rules, aggregates |
| `Infrastructure` | DB, email, S3, RabbitMQ |
| `Worker` | Background jobs, queue consumers |
| `Tests/*` | Unit/integration tests |

---

## After onboarding

- Join PRs: ensure migration check + tests pass.
- Large features: read [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md) before committing to multi-tenant / billing, etc.
- Update [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) when merging new features into the base.

**Documentation hub:** [`../README.md`](../README.md)
