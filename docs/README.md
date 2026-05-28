# Project documentation

Application source lives under **[`src/`](../src/)** (`src/Backend`, `src/Frontend`). This `docs/` tree is the human-facing handbook for those stacks.

## Quick entry points

| You are… | Read this first |
|---|---|
| New backend developer | [`backend/ONBOARDING.md`](./backend/ONBOARDING.md) |
| New Admin Console developer | [`admin/ONBOARDING.md`](./admin/ONBOARDING.md) |
| New Frontend (end-user app) developer | [`frontend/ONBOARDING.md`](./frontend/ONBOARDING.md) |
| Running or extending automated tests | [`test/ONBOARDING.md`](./test/ONBOARDING.md) |
| PM/tech lead checking what base already supports | [`backend/FEATURE_CATALOG.md`](./backend/FEATURE_CATALOG.md) |
| PM/tech lead — Admin features | [`admin/FEATURE_CATALOG.md`](./admin/FEATURE_CATALOG.md) |
| PM/tech lead — Frontend features | [`frontend/FEATURE_CATALOG.md`](./frontend/FEATURE_CATALOG.md) |
| Engineer implementing a new backend feature | [`backend/BASE_GUIDE.md`](./backend/BASE_GUIDE.md) + [`.cursor/rules/backend.mdc`](../.cursor/rules/backend.mdc) |
| Api / Worker / RabbitMQ / outbox | [`backend/MESSAGING_AND_WORKER.md`](./backend/MESSAGING_AND_WORKER.md) |
| Engineer debugging migrations/CI drift | [`backend/MIGRATION_WORKFLOW.md`](./backend/MIGRATION_WORKFLOW.md) |
| Operator setting up Gitea CI runners | [`backend/CI_GITEA_SETUP.md`](./backend/CI_GITEA_SETUP.md) |
| Engineer adding API or Playwright tests | [`test/EXTENSIBILITY_GUIDE.md`](./test/EXTENSIBILITY_GUIDE.md) + [`.cursor/rules/test.mdc`](../.cursor/rules/test.mdc) |

## Documentation areas

| Area | Location |
|------|----------|
| Backend docs hub | [`backend/README.md`](./backend/README.md) |
| Admin Console docs | [`admin/README.md`](./admin/README.md) |
| Frontend docs hub | [`frontend/README.md`](./frontend/README.md) |
| Tests (API integration + Playwright) | [`test/README.md`](./test/README.md) |
