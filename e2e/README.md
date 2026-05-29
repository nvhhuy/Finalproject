# End-to-end tests

Full handbook: [`docs/test/README.md`](../docs/test/README.md) · onboarding: [`docs/test/ONBOARDING.md`](../docs/test/ONBOARDING.md)

Two layers cover the monorepo features:

| Layer | Path | Scope |
|-------|------|--------|
| **API integration** | `src/Backend/Tests/Api.IntegrationTests` | Auth, profile, admin, health (Postgres via Testcontainers) |
| **Browser (Playwright)** | `e2e/` | Frontend + Admin UI flows (auth, app shell, support, settings, admin navigation) |

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

CI already runs this optional media suite in a dedicated `backend-media` job with MinIO.

Use an existing database instead of Testcontainers:

```bash
export TEST_DATABASE_CONNECTION_STRING="Host=localhost;Port=5432;Database=baseproject;Username=postgres;Password=postgres"
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

If API stays `000` or connection refused → start Postgres (`docker compose -f local/docker-compose.yml up -d postgres`).

Playwright starts the API, Frontend, and Admin dev servers automatically (unless you already run them — see `E2E_REUSE_SERVERS` below). **Postgres must be running**:

```bash
docker compose -f local/docker-compose.yml up -d postgres
```

By default, Playwright uses an isolated DB (`baseproject_e2e`) and creates it automatically from `global-setup`. The API webServer applies migrations on startup in `Development`.

Environment overrides:

| Variable | Default |
|----------|---------|
| `E2E_API_URL` | `http://localhost:5076` (macOS often reserves `:5000` for AirPlay) |
| `E2E_FRONTEND_URL` | `http://localhost:3000` |
| `E2E_ADMIN_URL` | `http://localhost:5174` |
| `E2E_ADMIN_EMAIL` | `admin@localhost.com` |
| `E2E_ADMIN_PASSWORD` | `Temp!ChangeMe123` |
| `E2E_SKIP_WEBSERVER` | set to `1` to use already-running servers |
| `E2E_REUSE_SERVERS` | `true` = Playwright reuses API/FE/Admin if already listening (must proxy to `E2E_API_URL`); default `false` starts fresh servers |
| `E2E_DATABASE_URL` | `Host=localhost;Port=5432;Database=baseproject_e2e;Username=postgres;Password=postgres` |
| `E2E_DATABASE_NAME` | `baseproject_e2e` (used by `global-setup` create/reset helpers) |
| `E2E_DROP_DB_AFTER_RUN` | `1` on CI, `0` on local (drop e2e DB in global teardown) |

Playwright naming convention is now domain-based (e.g. `frontend/support.spec.ts`, `admin/settings.spec.ts`) so file names match test intent.

### E2E stability (ports / CI)

| Variable | Default | Purpose |
|----------|---------|---------|
| `E2E_PREPARE_PORTS` | `0` local, `1` in CI via `run-playwright.sh` | Wait for or free ports `5076`, `3000`, `5174` before tests |
| `E2E_REUSE_SERVERS` | `false` in Playwright job | Skip starting duplicate API/FE/Admin when servers already running |
| `E2E_STABLE_PARALLEL` | `true` in CI | Allows parallel test files when combined with `E2E_WORKERS=2` |
| `E2E_WORKERS` | `2` in CI, `1` local | Playwright worker count; set `1` if parallel runs flake |
| `E2E_CLEAN_PORTS` | `0` local, `1` in CI prepare step | Kill listeners on 5076/3000/5174 only (not arbitrary ports) |

If Playwright fails with **port already in use**, stop local dev servers on those ports or run:

```bash
E2E_CLEAN_PORTS=1 bash tools/e2e/prepare-ports.sh
```

If you already run API/Frontend/Admin manually, use:

```bash
E2E_REUSE_SERVERS=true E2E_SKIP_WEBSERVER=1 npm test
```

Run a single app:

```bash
npm run test:frontend
npm run test:admin
```

Database lifecycle:

- `global-setup` ensures `baseproject_e2e` exists.
- On CI, `global-teardown` drops that DB by default (`E2E_DROP_DB_AFTER_RUN=1`) to keep runners clean.
- On local, DB is kept by default for faster reruns/debug (`E2E_DROP_DB_AFTER_RUN=0`).
