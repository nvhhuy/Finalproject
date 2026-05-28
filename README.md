# Final project

Monorepo layout:

| Path | Description |
|------|--------------|
| [`src/Backend/`](./src/Backend/) | .NET 8 API, Application, Infrastructure, Worker |
| [`src/Frontend/`](./src/Frontend/) | Vite / React client |
| [`docs/`](./docs/) | Documentation (`docs/backend/`, `docs/frontend/`, `docs/admin/`, `docs/test/`) |
| [`tools/`](./tools/) | Scripts (e.g. EF migrations) — not part of shipped app code |
| [`local/`](./local/) | Docker Compose for **Postgres + Redis + MinIO + OpenSearch** (run API/FE locally) |
| [`deploy/`](./deploy/) | Placeholder for future production Docker / CI notes |

**Quick start:** bring up dependencies with `local/docker-compose.yml`, configure `src/Backend/Api`, run `./tools/backend/migrate.sh`, then start the Api and Frontend.

**Tests:** API integration + Playwright — see [`docs/test/ONBOARDING.md`](./docs/test/ONBOARDING.md) (quick commands: [`e2e/README.md`](./e2e/README.md)).
