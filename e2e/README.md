# End-to-end tests

Full handbook: [`docs/test/README.md`](../docs/test/README.md) · onboarding: [`docs/test/ONBOARDING.md`](../docs/test/ONBOARDING.md)

Two layers cover the monorepo features:

| Layer | Path | Scope |
|-------|------|--------|
| **API integration** | `src/Backend/Tests/Api.IntegrationTests` | Auth, profile, admin, health (Postgres via Testcontainers) |
| **Browser (Playwright)** | `e2e/` | Frontend + Admin UI smoke flows |

## Prerequisites

- Docker (for API integration Testcontainers, or local Postgres)
- Node.js 20+
- .NET 8 SDK

Start dependencies (optional if using Testcontainers for API tests only):

```bash
docker compose -f local/docker-compose.yml up -d postgres
```

## API integration tests

```bash
# Unit tests (excludes integration)
dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"

# E2E API (spins up Postgres 16 via Testcontainers when Docker is available)
dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj

# Optional: media upload flow (requires MinIO)
INTEGRATION_MEDIA_ENABLED=true dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj --filter Media
```

Use an existing database instead of Testcontainers:

```bash
export TEST_DATABASE_CONNECTION_STRING="Host=localhost;Port=5432;Database=finalproject;Username=postgres;Password=postgres"
dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj
```

## Playwright UI tests

```bash
cd e2e
npm install
npm run install:browsers
npm test
```

**First run can look “stuck” for 2–5 minutes** — Playwright starts API → Frontend → Admin **in order** and waits until each URL responds before printing test results.

**Login fails with `DataProtection` / `EncryptedColumnDecryptionException`** — not CORS (browser calls same-origin `/api` via Next/Vite proxy). `global-setup` clears `TwoFactorSecret` and deletes the bootstrap admin so API re-seeds with `local/dataprotection-keys`.

**`Timed out waiting … from config.webServer`** — Postgres not running, or another process still holds `:5076` / `:3000` / `:5174`. Stop dev servers before `npm test`.

Check readiness:

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5076/health/live
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3000/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5174/
```

If API stays `000` or connection refused → start Postgres (`docker compose -f local/docker-compose.yml up -d postgres`) and run `./tools/backend/migrate.sh`.

Playwright starts the API, Frontend, and Admin dev servers automatically (unless you already run them — see `E2E_REUSE_SERVERS` below). **Postgres must be running** and migrated:

```bash
docker compose -f local/docker-compose.yml up -d postgres
./tools/backend/migrate.sh
```

Environment overrides:

| Variable | Default |
|----------|---------|
| `E2E_API_URL` | `http://localhost:5076` (macOS often reserves `:5000` for AirPlay) |
| `E2E_FRONTEND_URL` | `http://localhost:3000` |
| `E2E_ADMIN_URL` | `http://localhost:5174` |
| `E2E_ADMIN_EMAIL` | `admin@localhost.com` |
| `E2E_ADMIN_PASSWORD` | `Temp!ChangeMe123` |
| `E2E_SKIP_WEBSERVER` | set to `1` to use already-running servers |
| `E2E_REUSE_SERVERS` | set to `true` to reuse dev servers on :3000 / :5174 (must proxy to `E2E_API_URL`) |
| `E2E_DATABASE_URL` | local Postgres connection string for API webServer |

Run a single app:

```bash
npm run test:frontend
npm run test:admin
```
