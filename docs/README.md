# Project documentation

Application source lives under **[`src/`](../src/)** (`src/Backend`, `src/Frontend`, `src/Admin`). This `docs/` tree is the human-facing handbook for those stacks.

**Start here:** [`OVERVIEW.md`](./OVERVIEW.md) — architecture, production/security posture (honest assessment), local vs deploy, and reading order.

---

## Quick entry points

| You are… | Read this first |
|---|---|
| **Anyone new to the repo** | [`OVERVIEW.md`](./OVERVIEW.md) |
| New backend developer | [`backend/ONBOARDING.md`](./backend/ONBOARDING.md) |
| New Admin Console developer | [`admin/ONBOARDING.md`](./admin/ONBOARDING.md) |
| New Frontend (end-user app) developer | [`frontend/ONBOARDING.md`](./frontend/ONBOARDING.md) |
| Running or extending automated tests | [`test/ONBOARDING.md`](./test/ONBOARDING.md) |
| PM/tech lead — what the backend supports | [`backend/FEATURE_CATALOG.md`](./backend/FEATURE_CATALOG.md) |
| PM/tech lead — production readiness & scale | [`backend/PRODUCTION_READINESS_REPORT.md`](./backend/PRODUCTION_READINESS_REPORT.md) |
| PM/tech lead — Admin features | [`admin/FEATURE_CATALOG.md`](./admin/FEATURE_CATALOG.md) |
| PM/tech lead — Frontend features | [`frontend/FEATURE_CATALOG.md`](./frontend/FEATURE_CATALOG.md) |
| Engineer implementing a new backend feature | [`backend/BASE_GUIDE.md`](./backend/BASE_GUIDE.md) + [`.cursor/rules/backend.mdc`](../.cursor/rules/backend.mdc) |
| Api / Worker / RabbitMQ / outbox | [`backend/MESSAGING_AND_WORKER.md`](./backend/MESSAGING_AND_WORKER.md) |
| DataProtection key ring (2FA, cookies) | [`backend/DATA_PROTECTION.md`](./backend/DATA_PROTECTION.md) |
| Engineer debugging migrations/CI drift | [`backend/MIGRATION_WORKFLOW.md`](./backend/MIGRATION_WORKFLOW.md) |
| Operator setting up Gitea CI runners | [`backend/CI_GITEA_SETUP.md`](./backend/CI_GITEA_SETUP.md) |
| Operator planning production deploy | [`deploy/README.md`](./deploy/README.md) → [`deploy/CHECKLIST.md`](./deploy/CHECKLIST.md) |
| Engineer adding API or Playwright tests | [`test/EXTENSIBILITY_GUIDE.md`](./test/EXTENSIBILITY_GUIDE.md) + [`.cursor/rules/test.mdc`](../.cursor/rules/test.mdc) |

---

## Production & security (read before go-live)

| Topic | Document |
|-------|----------|
| Honest production verdict + security summary | [`OVERVIEW.md`](./OVERVIEW.md) § Production-ready |
| Backend scale, idempotency, gaps | [`backend/PRODUCTION_READINESS_REPORT.md`](./backend/PRODUCTION_READINESS_REPORT.md) |
| DataProtection keys (required in prod) | [`backend/DATA_PROTECTION.md`](./backend/DATA_PROTECTION.md) |
| Deploy topology & edge security | [`deploy/ARCHITECTURE.md`](./deploy/ARCHITECTURE.md) |
| Pre/post deploy checklist | [`deploy/CHECKLIST.md`](./deploy/CHECKLIST.md) |
| Env file strategy | [`deploy/ENVIRONMENT_STRATEGY.md`](./deploy/ENVIRONMENT_STRATEGY.md) |

---

## Documentation areas

| Area | Location |
|------|----------|
| **Codebase overview (hub)** | [`OVERVIEW.md`](./OVERVIEW.md) |
| Backend docs hub | [`backend/README.md`](./backend/README.md) |
| Admin Console docs | [`admin/README.md`](./admin/README.md) |
| Frontend docs hub | [`frontend/README.md`](./frontend/README.md) |
| Deploy docs hub | [`deploy/README.md`](./deploy/README.md) |
| Tests (API integration + Playwright) | [`test/README.md`](./test/README.md) |
