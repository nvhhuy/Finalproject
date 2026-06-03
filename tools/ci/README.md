# Run CI locally

When GitHub Actions free minutes run out (or you want faster feedback), run the same checks as [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) on your machine.

## Quick start

From the **repository root**:

```bash
# Prerequisites: Docker running, .NET 8 SDK, Node 24 (nvm use)
chmod +x tools/ci/run-ci-local.sh
./tools/ci/run-ci-local.sh
```

Single job (faster):

```bash
./tools/ci/run-ci-local.sh backend
./tools/ci/run-ci-local.sh media
./tools/ci/run-ci-local.sh playwright
```

List jobs:

```bash
./tools/ci/run-ci-local.sh --list
```

If Postgres / Redis / MinIO are **already** up (`docker compose -f local/docker-compose.yml up -d`):

```bash
CI_SKIP_DOCKER=1 ./tools/ci/run-ci-local.sh backend media
```

## What each job runs

| Job | GitHub job name | Needs |
|-----|-----------------|--------|
| `admin` | Admin quality checks | Node 24, `npm ci` in `src/Admin` |
| `frontend` | Frontend quality checks | Node 24, `npm ci` in `src/Frontend` |
| `backend` | Backend tests | .NET 8, Postgres |
| `media` | Backend media integration | .NET 8, Postgres, MinIO (`INTEGRATION_MEDIA_ENABLED`) |
| `playwright` | Playwright E2E | .NET 8, Node 24, Postgres, Redis |

The script starts `local/docker-compose.yml` services when needed (`postgres`, `redis`, `minio`). Integration DB names match CI: `baseproject_integration`, `baseproject_media_integration`.

## Prerequisites

| Tool | Version | Notes |
|------|---------|--------|
| Docker | — | Desktop or engine; required for `backend`, `media`, `playwright` |
| .NET SDK | 8.x | `dotnet test` |
| Node.js | **24** (CI) | `nvm use` — repo [`.nvmrc`](../../.nvmrc) |
| `curl` | — | MinIO health check (`media` job) |
| `createdb` (optional) | — | Host `postgresql` client; otherwise DBs are created via `docker compose exec` |

Start infra once (optional — the script can do it for you):

```bash
docker compose -f local/docker-compose.yml up -d postgres redis minio minio-init
```

## Parity with GitHub Actions

This runner intentionally mirrors CI env vars and commands, for example:

- `IntegrationTesting__DisableRateLimiting=true`
- `TEST_DATABASE_CONNECTION_STRING` for integration tests
- MinIO: `ObjectStorage__*` + `INTEGRATION_MEDIA_ENABLED=true` for media
- Playwright: `CI=true`, `E2E_POSTGRES_CLI=1`, `E2E_PREPARE_PORTS=1`, via [`tools/e2e/run-playwright.sh`](../e2e/run-playwright.sh)

Differences from cloud CI:

- Runs on your OS (macOS/Linux), not `ubuntu-latest`
- Uses `local/docker-compose.yml` instead of GitHub `services:` containers
- `npm audit` failures are warned, not failed (same as `continue-on-error: true` in workflow)

## Alternatives

### Run only what you need day-to-day

```bash
./tools/ci/run-ci-local.sh admin frontend backend   # skip slow E2E
./tools/e2e/run-api-integration.sh                  # API integration only
./tools/e2e/run-playwright.sh                         # Playwright only
```

### Gitea / self-hosted Actions

For a remote runner that executes workflow YAML (not this shell script), see [`docs/backend/CI_GITEA_SETUP.md`](../../docs/backend/CI_GITEA_SETUP.md) and [`gitea-runner.compose.yml`](./gitea-runner.compose.yml).

### `act` (GitHub Actions emulator)

You can run workflow jobs with [nektos/act](https://github.com/nektos/act) if installed:

```bash
act pull_request -j backend --container-architecture linux/amd64
```

`act` needs Docker, pulls large images, and may need secrets/network tweaks. `run-ci-local.sh` is the maintained, repo-native path.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Docker not running | Start Docker Desktop |
| Node &lt; 20 | `nvm install && nvm use` in repo root |
| Port 5432 in use | Stop other Postgres or change `PGPORT` + compose port mapping |
| Media test skipped locally | Run `./tools/ci/run-ci-local.sh media` (sets `INTEGRATION_MEDIA_ENABLED`) |
| Playwright port conflicts | Script sets `E2E_PREPARE_PORTS=1`; close dev servers on 3000/5174/5000 |
| MinIO not ready | `docker compose -f local/docker-compose.yml up -d minio minio-init` |

More test context: [`docs/test/ONBOARDING.md`](../../docs/test/ONBOARDING.md).
