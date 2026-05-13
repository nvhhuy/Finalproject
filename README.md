# Final project

Monorepo layout:

| Path | Description |
|------|--------------|
| [`src/Backend/`](./src/Backend/) | .NET 8 API, Application, Infrastructure, Worker |
| [`src/Frontend/`](./src/Frontend/) | Vite / React client |
| [`docs/`](./docs/) | Documentation index (`docs/backend/`, `docs/frontend/`) |
| [`tools/`](./tools/) | Scripts (e.g. EF migrations) — not part of shipped app code |
| [`local/`](./local/) | Docker Compose for **Postgres + Redis + MinIO + OpenSearch** (run API/FE locally) |
| [`deploy/`](./deploy/) | Placeholder for future production Docker / CI notes |

**Quick start:** bring up dependencies with `local/docker-compose.yml`, configure `src/Backend/Api`, run `./tools/backend/migrate.sh`, then start the Api and Frontend.
