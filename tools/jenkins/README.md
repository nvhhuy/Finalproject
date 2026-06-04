# Jenkins CI/CD setup

Pipeline entry point: [`Jenkinsfile`](../../Jenkinsfile) at the repository root.

This implements **Option 1** from [`docs/deploy/OPTIONS.md`](../../docs/deploy/OPTIONS.md):

1. Run tests (mirrors GitHub Actions via `tools/ci/run-ci-local.sh`)
2. **Parallel** SonarQube analysis — Backend / Frontend / Admin (3 Sonar projects)
3. **Parallel** build & push Docker images to Harbor — `api`, `worker`, `migrate`, `frontend`, `admin`
4. SSH deploy on VPS (`migrate.sh` → `up.sh`)
5. Public HTTPS smoke test + email notification

Supporting scripts live in [`scripts/`](./scripts/).

## Pipeline parameters

| Parameter | Values | Purpose |
|-----------|--------|---------|
| `TARGET_ENV` | `staging`, `production` | Selects deploy env + smoke hostnames |
| `PIPELINE_ACTION` | `deploy`, `rollback`, `smoke-only` | Control which stages run |
| `IMAGE_TAG` | string (optional) | Harbor tag; empty → `BUILD_NUMBER`. **Required for rollback** |
| `SKIP_TESTS` | boolean | Skip test stage |
| `SKIP_SONAR` | boolean | Skip SonarQube |
| `SKIP_DEPLOY` | boolean | Build/push only, no SSH deploy |

### Typical flows

**Normal release (staging or production)**

- `PIPELINE_ACTION` = `deploy`
- `IMAGE_TAG` = empty (uses build `#42` → images `harbor.../api:42`, etc.)
- All skips = false

**Rollback to build #38**

- `PIPELINE_ACTION` = `rollback`
- `IMAGE_TAG` = `38`
- Skips tests/sonar/build; **skips DB migrate** — only redeploys containers with the old tag

**Verify production after incident**

- `PIPELINE_ACTION` = `smoke-only`
- Runs HTTP checks + email only

## Configure Jenkins

### 1. Plugins

Install on Jenkins:

- **Pipeline**
- **Git**
- **SSH Agent**
- **Email Extension**
- **SonarQube Scanner** (optional — quality gate uses Sonar API via `sonar-quality-gate.sh`)

System email (for `emailext`): **Manage Jenkins → System → Extended E-mail Notification**.

### 2. SonarQube server

**Manage Jenkins → System → SonarQube servers**

| Field | Example |
|-------|---------|
| Name | `SonarQube` (must match scanner config if using `withSonarQubeEnv`) |
| Server URL | same as `SONAR_HOST_URL` in Jenkinsfile |
| Server authentication token | Sonar user token (also stored as credential below) |

Enable **Environment variables** injection if using SonarQube plugin wrappers.

Create **three SonarQube projects** (or let first scan create them):

| Jenkins env | Default project key |
|-------------|---------------------|
| `SONAR_PROJECT_KEY_BACKEND` | `baseproject-backend` |
| `SONAR_PROJECT_KEY_FRONTEND` | `baseproject-frontend` |
| `SONAR_PROJECT_KEY_ADMIN` | `baseproject-admin` |

Apply the same quality gate profile to all three projects in Sonar UI.

### 3. Credentials

**Manage Jenkins → Credentials → Add**

| ID | Type | Used for |
|----|------|----------|
| `harbor-credentials` | Username with password | Harbor `docker login` + push |
| `sonar-token` | Secret text | SonarQube API token |
| `deploy-ssh-key` | SSH Username with private key | VPS deploy user |

Credential IDs must match the `*_CREDENTIALS_ID` variables in the `environment` block of [`Jenkinsfile`](../../Jenkinsfile).

### 4. Edit `environment` in Jenkinsfile

Update placeholders for your infrastructure:

```groovy
HARBOR_REGISTRY = 'harbor.yourcompany.com/baseproject'
SONAR_HOST_URL = 'https://sonar.yourcompany.com'
SONAR_PROJECT_KEY_BACKEND = 'baseproject-backend'
SONAR_PROJECT_KEY_FRONTEND = 'baseproject-frontend'
SONAR_PROJECT_KEY_ADMIN = 'baseproject-admin'

DEPLOY_HOST_STAGING = 'staging-vps.yourcompany.com'
DEPLOY_HOST_PRODUCTION = 'prod-vps.yourcompany.com'
DEPLOY_REPO_PATH = '/opt/baseproject'
DEPLOY_SSH_USER = 'deploy'

SMOKE_APP_HOST_STAGING = 'app.staging.yourcompany.com'
SMOKE_ADMIN_HOST_STAGING = 'admin.staging.yourcompany.com'
SMOKE_APP_HOST_PRODUCTION = 'app.yourcompany.com'
SMOKE_ADMIN_HOST_PRODUCTION = 'admin.yourcompany.com'

NOTIFY_EMAIL = 'ops@yourcompany.com'
```

Set `SONAR_QUALITY_GATE = 'false'` to skip quality gate polling while tuning Sonar rules.

### Parallel stages

The Jenkinsfile runs these stages **in parallel** (same agent, multiple processes):

| Stage | Parallel branches |
|-------|-------------------|
| SonarQube | Backend (.NET), Frontend (scanner CLI), Admin (scanner CLI) |
| Build & Push | api, worker, migrate, frontend, admin |

`harbor-login.sh` runs once before parallel image builds. Quality gate waits for all 3 Sonar projects via API (`sonar-quality-gate.sh`).

For local testing without Jenkins:

```bash
export SONAR_HOST_URL=... SONAR_TOKEN=... IMAGE_REGISTRY=... IMAGE_TAG=test HARBOR_USER=... HARBOR_PASSWORD=...
./tools/jenkins/scripts/sonar-scan.sh      # parallel scans + gate
./tools/jenkins/scripts/build-push.sh     # parallel build/push
```

### 5. Jenkins agent tools

The agent running the pipeline needs:

| Tool | Version | Notes |
|------|---------|-------|
| Docker | — | build/push images; Sonar frontend/admin use `sonarsource/sonar-scanner-cli` |
| .NET SDK | 8.x | tests + Sonar backend |
| Node.js | 24 | matches repo `.nvmrc` (tests) |
| Python 3 | — | `sonar-quality-gate.sh` JSON parsing |
| `curl` | — | smoke tests |
| Git | — | checkout |

Optional: pre-install `dotnet-sonarscanner` globally on the agent (otherwise `sonar-scan.sh` installs it).

### 6. Create Pipeline job

1. **New Item → Pipeline**
2. **Pipeline → Definition**: *Pipeline script from SCM*
3. **SCM**: Git → repository URL → branch `main`
4. **Script Path**: `Jenkinsfile`
5. Save → **Build with Parameters**

## VPS prerequisites (one-time)

On each deploy host (`DEPLOY_HOST_*`):

```bash
# Clone repo (scripts only; images come from Harbor)
sudo mkdir -p /opt/baseproject && sudo chown deploy:deploy /opt/baseproject
git clone <repo-url> /opt/baseproject

# Env files (never commit real secrets)
cp deploy/env/shared/app.common.env.example deploy/env/shared/app.common.env
cp deploy/env/shared/traefik.env.example deploy/env/shared/traefik.env
cp deploy/env/staging/infra.env.example deploy/env/staging/infra.env
cp deploy/env/staging/app.env.example deploy/env/staging/app.env
# Edit APP_HOST, ADMIN_HOST, IMAGE_REGISTRY=harbor.yourcompany.com/baseproject, secrets

./deploy/scripts/traefik-up.sh
./deploy/scripts/infra-up.sh staging
```

Ensure `app.common.env` has `IMAGE_REGISTRY` matching `HARBOR_REGISTRY` in Jenkinsfile.

Harbor: create robot account with push/pull on project `baseproject`. Jenkins passes `HARBOR_USER`/`HARBOR_PASSWORD` on each deploy via `deploy-remote.sh` (or run `deploy/scripts/harbor-login.sh` once on the VPS for manual ops).

## Image tagging & rollback

Default tag = **`BUILD_NUMBER`** (e.g. Jenkins build `#42` → `api:42`, `worker:42`, …).

To rollback:

1. Find previous successful build number in Jenkins (or `deploy/releases/<env>/deploy.log` on VPS)
2. Run pipeline: `PIPELINE_ACTION=rollback`, `IMAGE_TAG=42`, `TARGET_ENV=production`
3. Pipeline skips build and **migrate**; VPS runs `deploy-app.sh` (pull + up only) + `smoke.sh` + writes `deploy/releases/<env>/deploy.log`
4. Jenkins **Smoke Test** stage is skipped (smoke already ran on VPS); use `smoke-only` action to re-check from Jenkins agent

DB schema is **not** rolled back — only application containers revert. Restore from backup if schema must be reverted.

## Scripts reference

| Script | Role |
|--------|------|
| [`scripts/harbor-login.sh`](./scripts/harbor-login.sh) | One-time `docker login` before parallel builds |
| [`scripts/build-push-one.sh`](./scripts/build-push-one.sh) | Build + push **one** image (`api`, `worker`, …) |
| [`scripts/build-push.sh`](./scripts/build-push.sh) | All 5 images in parallel (CLI fallback) |
| [`scripts/sonar-backend.sh`](./scripts/sonar-backend.sh) | Sonar .NET (`Backend.sln` — Api + Worker) |
| [`scripts/sonar-frontend.sh`](./scripts/sonar-frontend.sh) | Sonar Frontend (TypeScript) |
| [`scripts/sonar-admin.sh`](./scripts/sonar-admin.sh) | Sonar Admin (TypeScript) |
| [`scripts/sonar-quality-gate.sh`](./scripts/sonar-quality-gate.sh) | Poll quality gate for all 3 projects |
| [`scripts/sonar-scan.sh`](./scripts/sonar-scan.sh) | All Sonar scans in parallel + gate (CLI fallback) |
| [`scripts/deploy-remote.sh`](./scripts/deploy-remote.sh) | SSH → `deploy-app.sh` + `smoke.sh` (Harbor login + `SKIP_MIGRATE` on rollback) |
| [`scripts/smoke-http.sh`](./scripts/smoke-http.sh) | Public HTTPS checks from Jenkins (`smoke-only` / `SKIP_DEPLOY`) |

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Harbor push 401 | Check `harbor-credentials`, robot account permissions |
| Sonar quality gate fails | Fix issues in Sonar UI or set `SONAR_QUALITY_GATE=false`; check all 3 project keys |
| Parallel build OOM | Reduce parallel jobs or give Jenkins agent more RAM (5 Docker builds at once) |
| Smoke test TLS error | DNS not pointing to Traefik, or ACME not issued yet |
| Deploy SSH fails | Verify `deploy-ssh-key`, `DEPLOY_HOST_*`, firewall |
| `Missing app.common.env` on VPS | Copy env templates on server (see above) |
| Tests slow | Use dedicated Jenkins agent with Docker; skip Playwright in Jenkins (`media` only in default Jenkinsfile — add `playwright` to test stage if needed) |

## Relation to GitHub Actions

- **GitHub Actions** (`.github/workflows/ci.yml`) — PR/push quality gates on GitHub
- **Jenkins** — release deploy to staging/production with Harbor + VPS

Both can run the same test script: `tools/ci/run-ci-local.sh`.

More deploy context: [`deploy/README.md`](../../deploy/README.md), [`docs/deploy/CHECKLIST.md`](../../docs/deploy/CHECKLIST.md).
