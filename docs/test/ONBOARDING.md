# Testing onboarding

Get **API integration** and **Playwright** tests running locally in ~20 minutes.

---

## Where tests live

```
BaseProject/
├── src/Backend/Tests/Api.IntegrationTests/   ← HTTP integration (xUnit)
├── e2e/                                      ← Playwright (Frontend + Admin)
├── tools/e2e/                                ← Convenience scripts
└── docs/test/                                ← this documentation
```

---

## Recommended reading path

| Order | Document | Time | Goal |
|-------|----------|------|------|
| 1 | **This file** | 20 min | Run both test layers |
| 2 | [`TEST_CATALOG.md`](./TEST_CATALOG.md) | 10 min | Know what is covered |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | 15 min | Fixtures, auth, ports |
| 4 | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) | When adding tests | Add specs safely |
| 5 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | When using AI | Test coding rules |

---

## Day 1 — checklist

### 1. Prerequisites

- [ ] [Docker](https://www.docker.com/) (Postgres for Playwright; Testcontainers for API integration)
- [ ] [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [ ] [Node.js ≥ 20](https://nodejs.org/) (Playwright + dev servers)
- [ ] Playwright browsers: `cd e2e && npm run install:browsers`

### 2. Start Postgres and migrate

From repo root:

```bash
docker compose -f local/docker-compose.yml up -d postgres
```

Playwright UI tests use a dedicated DB (`baseproject_e2e`) by default and create it automatically in `e2e/global-setup.ts`. This keeps local dev data (`baseproject`) clean.

### 3. Run API integration tests

```bash
./tools/e2e/run-api-integration.sh
```

Or directly:

```bash
dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj
```

**Without Docker:** point at an existing database:

```bash
export TEST_DATABASE_CONNECTION_STRING="Host=localhost;Port=5432;Database=baseproject;Username=postgres;Password=postgres"
dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj
```

**Optional media test** (needs MinIO):

```bash
INTEGRATION_MEDIA_ENABLED=true dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj --filter Media
```

### 4. Run Playwright UI tests

```bash
./tools/e2e/run-playwright.sh
```

First run may take **2–5 minutes**: Playwright starts API → Frontend → Admin and waits for health URLs.

**Stop other dev servers** on ports `5076`, `3000`, and `5174` before running, or set `E2E_SKIP_WEBSERVER=1` if you start them yourself (see env table below).

### 5. Run unit tests only (exclude integration)

```bash
dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"
```

---

## Default credentials and ports

| Item | Value |
|------|--------|
| Admin seed email | `admin@localhost.com` |
| Admin seed password | `Temp!ChangeMe123` |
| API (Playwright webServer) | `http://localhost:5076` (avoids macOS AirPlay on `:5000`) |
| Frontend | `http://localhost:3000` |
| Admin Console | `http://localhost:5174` |
| DataProtection keys (e2e API) | `local/dataprotection-keys/` |

---

## Environment variables

### Playwright (`e2e/`)

| Variable | Default | Purpose |
|----------|---------|---------|
| `E2E_API_URL` | `http://localhost:5076` | API base for webServer health check |
| `E2E_PREPARE_PORTS` | `0` | `1` in CI (via `tools/e2e/run-playwright.sh`) — free/wait for ports 5076/3000/5174 |
| `E2E_REUSE_SERVERS` | `false` | Skip Playwright webServer when stack already running |
| `E2E_STABLE_PARALLEL` | `true` in CI | Parallel test files when combined with `E2E_WORKERS=2` |
| `E2E_WORKERS` | `2` in CI, `1` local | Playwright worker count; set `1` if parallel runs flake |
| `E2E_CLEAN_PORTS` | `0` local, `1` in CI prepare step | Stop listeners on 5076/3000/5174 only |
| `E2E_FRONTEND_URL` | `http://localhost:3000` | Frontend baseURL |
| `E2E_ADMIN_URL` | `http://localhost:5174` | Admin baseURL |
| `E2E_ADMIN_EMAIL` | `admin@localhost.com` | Login helper |
| `E2E_ADMIN_PASSWORD` | `Temp!ChangeMe123` | Login helper |
| `E2E_DATABASE_URL` | `Host=localhost;Port=5432;Database=baseproject_e2e;Username=postgres;Password=postgres` | API `webServer` env |
| `E2E_DATABASE_NAME` | `baseproject_e2e` | DB name used by `global-setup` create/reset |
| `E2E_DROP_DB_AFTER_RUN` | `1` on CI, `0` on local | Drop e2e DB in `global-teardown` |
| `E2E_SKIP_WEBSERVER` | unset | `1` = do not start API/FE/Admin (you run them) |
| `E2E_REUSE_SERVERS` | `false` | `true` = reuse existing dev servers on configured ports |
| `E2E_WEBSERVER_TIMEOUT_MS` | `300000` | Max wait for dev servers |
| `E2E_POSTGRES_HOST` / `E2E_POSTGRES_PORT` | `localhost` / `5432` | `global-setup` connectivity check |
| `E2E_POSTGRES_USER` / `E2E_POSTGRES_PASSWORD` | `postgres` / `postgres` | Direct `psql`/`createdb` when `E2E_POSTGRES_CLI=1` |
| `E2E_POSTGRES_CLI` | `1` on CI | Use `psql` instead of `docker compose exec` |

### API integration

| Variable | Purpose |
|----------|---------|
| `TEST_DATABASE_CONNECTION_STRING` | Use existing Postgres instead of Testcontainers |
| `INTEGRATION_MEDIA_ENABLED` | `true` to run MinIO-dependent media test |

---

## Troubleshooting

### Playwright: `Timed out waiting … from config.webServer`

- Postgres not running → `docker compose -f local/docker-compose.yml up -d postgres`
- Port conflict (another dev server on 5076/3000/5174) → run `bash tools/e2e/prepare-ports.sh` or `E2E_CLEAN_PORTS=1 bash tools/e2e/prepare-ports.sh`
- Reuse already-running stack without health check → `E2E_REUSE_SERVERS=true E2E_SKIP_WEBSERVER=1 npm test` (see `tools/e2e/wait-for-e2e-health.sh`)
- Increase timeout: `E2E_WEBSERVER_TIMEOUT_MS=600000 npm test`

### Login API 500 / `EncryptedColumnDecryptionException`

Not CORS. Encrypted `TwoFactorSecret` in DB was written with **different DataProtection keys** than the e2e API uses.

`e2e/global-setup.ts` resets 2FA columns and deletes bootstrap admin in the dedicated e2e database when `webServer` starts. If it fails (no Docker exec), run manually:

```bash
docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d baseproject_e2e -c \
  'UPDATE "Users" SET "TwoFactorSecret" = NULL; DELETE FROM "Users" WHERE "Email" = '"'"'admin@localhost.com'"'"';'
```

Ensure API uses `local/dataprotection-keys` (Playwright `webServer` sets `DataProtection__KeysDirectory`).

### E2E DB cleanup policy

- Default local: keep `baseproject_e2e` for faster reruns/debug.
- Default CI: drop `baseproject_e2e` after run to avoid leftover state.
- Override anytime with `E2E_DROP_DB_AFTER_RUN=1` (always drop) or `0` (never drop).

### Admin authenticated tests redirect to `/login`

Admin stores **accessToken in Zustand memory only** — Playwright `storageState` cannot restore it. Authenticated admin specs must call `loginAdminConsole()` in `beforeEach` (see `e2e/tests/admin/*.spec.ts`).

### Frontend authenticated tests fail on setup

Frontend tokens live in **localStorage** (`fp.auth.tokens`) — `frontend-setup` project saves `playwright/.auth/frontend.json`. Re-run setup if tokens expire.

### Readiness checks

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5076/health/live
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3000/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5174/
```

---

## CI (GitHub Actions)

Workflow: [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) on pull requests and pushes to `main` / `master`.

| Job | What it runs |
|-----|----------------|
| **Backend tests** | Unit tests + `Api.IntegrationTests` against Postgres 16 service |
| **Backend media integration** | `MediaIntegrationTests` with MinIO + `INTEGRATION_MEDIA_ENABLED=true` |
| **Playwright E2E** | `./tools/e2e/run-playwright.sh` (Node 20, isolated DB `baseproject_e2e`, dropped after run) |

Local before push: `nvm use` then `./tools/e2e/run-playwright.sh` and `./tools/e2e/run-api-integration.sh`.

Never point tests at production databases or shared DataProtection key paths.

For self-hosted Gitea runners see [`docs/backend/CI_GITEA_SETUP.md`](../backend/CI_GITEA_SETUP.md).

---

## Next steps

- Adding coverage for a new feature → [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md)
- Understanding fixtures → [`ARCHITECTURE.md`](./ARCHITECTURE.md)
