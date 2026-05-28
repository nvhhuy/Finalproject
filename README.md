# Final project

Full-stack monorepo: .NET 8 API + Worker, Next.js frontend, React admin console, Docker-based local infra and production deploy assets.

**Documentation hub:** [`docs/OVERVIEW.md`](./docs/OVERVIEW.md) — architecture, honest production/security assessment, and onboarding paths.

---

## Repository layout

| Path | Description |
|------|-------------|
| [`src/Backend/`](./src/Backend/) | .NET 8 API, Application, Domain, Infrastructure, Worker |
| [`src/Frontend/`](./src/Frontend/) | Next.js / React end-user app |
| [`src/Admin/`](./src/Admin/) | Vite + React admin console |
| [`docs/`](./docs/) | Handbook (`backend/`, `frontend/`, `admin/`, `test/`, `deploy/`) |
| [`local/`](./local/) | Docker Compose for **local** Postgres, Redis, RabbitMQ, MinIO, … (dev credentials only) |
| [`deploy/`](./deploy/) | Production Dockerfiles, compose, env templates, scripts — see [`deploy/README.md`](./deploy/README.md) |
| [`e2e/`](./e2e/) | Playwright tests |
| [`tools/`](./tools/) | Migrate and test helper scripts |

---

## Production-ready?

**Architecture: yes — operator setup: required.**

The codebase is a **production-viable baseline**: layered backend, Api/Worker split, transactional outbox, security hardening in the API (HSTS, `AllowedHosts` guard, JWT, idempotency, rate limits). It is **not** fully turnkey: you must configure secrets, persistent DataProtection keys, RabbitMQ/Redis as needed, run migrations, and follow [`docs/deploy/CHECKLIST.md`](./docs/deploy/CHECKLIST.md).

Details: [`docs/OVERVIEW.md`](./docs/OVERVIEW.md) and [`docs/backend/PRODUCTION_READINESS_REPORT.md`](./docs/backend/PRODUCTION_READINESS_REPORT.md).

---

## Quick start (local)

```bash
docker compose -f local/docker-compose.yml up -d
./tools/backend/migrate.sh
dotnet run --project src/Backend/Api
dotnet run --project src/Backend/Worker
```

In separate terminals:

```bash
cd src/Frontend && npm install && npm run dev   # http://localhost:3000
cd src/Admin && npm install && npm run dev      # http://localhost:5174
```

**Dev admin (Development seed):** `admin@localhost.com` / `Temp!ChangeMe123` — see [`docs/backend/ONBOARDING.md`](./docs/backend/ONBOARDING.md).

---

## Tests

API integration + Playwright: [`docs/test/ONBOARDING.md`](./docs/test/ONBOARDING.md) — quick commands in [`e2e/README.md`](./e2e/README.md).

```bash
./tools/e2e/run-api-integration.sh
./tools/e2e/run-playwright.sh
```

---

## Deploy (staging / production)

```bash
./deploy/scripts/traefik-up.sh <env>
./deploy/scripts/infra-up.sh <env>
./deploy/scripts/build.sh <env>
./deploy/scripts/migrate.sh <env>
IMAGE_TAG=<tag> ./deploy/scripts/up.sh <env>
./deploy/scripts/smoke.sh <env>
```

Full playbook: [`docs/deploy/README.md`](./docs/deploy/README.md).
