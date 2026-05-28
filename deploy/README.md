# Deploy assets

Docker images, Compose stacks, Traefik routing, and env templates for **src/** applications.

Docs: [`docs/deploy/README.md`](../docs/deploy/README.md)

## Layout

```text
deploy/
  docker/
    api/Dockerfile          # src/Backend/Api
    worker/Dockerfile       # src/Backend/Worker
    frontend/Dockerfile     # src/Frontend (Next.js standalone)
    admin/Dockerfile        # src/Admin (Vite + nginx)
    migrate/Dockerfile      # EF migrations (one-shot)
  compose/
    traefik.yml             # Edge TLS + routing
    infra.yml               # Postgres, Redis, RabbitMQ, MinIO, ClamAV
                            # + optional OpenSearch, Prometheus, Grafana, Uptime Kuma
    stack.yml               # Api, Worker, Frontend, Admin
  monitoring/
    prometheus/             # scrape config + blackbox config
    grafana/                # provisioning + dashboards + alert rules
  env/
    shared/                 # Hostnames, image registry
    staging/                # Staging secrets template
    production/             # Production secrets template
  scripts/
    traefik-up.sh
    infra-up.sh
    infra-down.sh
    build.sh
    manifest.sh
    migrate.sh
    smoke.sh
    up.sh
```

## First-time staging on a VPS

1. Copy env templates (never commit real `*.env`):

```bash
cp deploy/env/shared/app.common.env.example deploy/env/shared/app.common.env
cp deploy/env/shared/traefik.env.example deploy/env/shared/traefik.env
cp deploy/env/staging/infra.env.example deploy/env/staging/infra.env
cp deploy/env/staging/app.env.example deploy/env/staging/app.env
# Edit APP_HOST, ADMIN_HOST, passwords, JWT_SECRET (>= 32 chars)
```

2. Create Docker network and start Traefik:

```bash
./deploy/scripts/traefik-up.sh
```

3. Start infrastructure stack:

```bash
./deploy/scripts/infra-up.sh staging
# Optional observability:
# ./deploy/scripts/infra-up.sh staging --profile observability --profile monitoring
```

4. Build images (Option 2 manual) or pull from Harbor (Option 1 CI):

```bash
./deploy/scripts/build.sh staging
```

5. Migrate database, then start app:

```bash
./deploy/scripts/migrate.sh staging
./deploy/scripts/up.sh staging
./deploy/scripts/smoke.sh staging
```

6. Verify:

- Api health internally: `docker compose --env-file deploy/env/shared/app.common.env --env-file deploy/env/staging/app.env -f deploy/compose/stack.yml exec api curl -fsS http://127.0.0.1:8080/health/ready`
- Browser path check: `https://<APP_HOST>/api/v1/...`

## Routing (Traefik)

| URL | Service |
|-----|---------|
| `https://APP_HOST/` | Frontend (Next) |
| `https://APP_HOST/api/*` | Api |
| `https://ADMIN_HOST/` | Admin static |
| `https://ADMIN_HOST/api/*` | Api |
| `https://GRAFANA_HOST/` | Grafana (monitoring profile, Traefik basic auth) |
| `https://UPTIME_KUMA_HOST/` | Uptime Kuma (monitoring profile, Traefik basic auth) |
| `https://OPENSEARCH_DASHBOARDS_HOST/` | OpenSearch Dashboards (observability profile, Traefik basic auth) |

Set `OBSERVABILITY_BASIC_AUTH` in `app.common.env` before enabling those profiles. Prometheus has no public route.

Worker has **no** public Traefik route.

`smoke.sh` fail-fast checks: internal `/health/ready`, public FE/Admin `200`, public `/api/v1/auth/login` empty POST `400` on both app and admin hosts.

## Option 1 (CI / Harbor)

Harbor is required in this option. Jenkins (or similar) should:

1. `docker build` / `docker compose build` with `IMAGE_REGISTRY` + immutable `IMAGE_TAG`
2. Push to Harbor
3. On server: `IMAGE_TAG=v1.2.3 ./deploy/scripts/migrate.sh production`
4. `IMAGE_TAG=v1.2.3 ./deploy/scripts/up.sh production`
5. `IMAGE_TAG=v1.2.3 ./deploy/scripts/smoke.sh production`
6. Infra can be long-lived (`infra-up` once), not redeployed on every release.

## Option 2 (manual SSH)

No Harbor in this option. Recommended flow:

1. `git checkout <release-sha>`
2. Build local tags:
   - `IMAGE_REGISTRY=baseproject IMAGE_TAG=20260528-1115-<sha7> ./deploy/scripts/build.sh production`
3. Deploy:
   - `IMAGE_REGISTRY=baseproject IMAGE_TAG=20260528-1115-<sha7> ./deploy/scripts/migrate.sh production`
   - `IMAGE_REGISTRY=baseproject IMAGE_TAG=20260528-1115-<sha7> ./deploy/scripts/up.sh production`
4. Smoke gate:
   - `./deploy/scripts/smoke.sh production`
5. Save manifest file (automatic after smoke passes) for rollback:
   - `deploy/releases/production/deploy.log`

Example log line:

```json
{"time":"2026-05-28T03:24:18Z","env":"production","status":"smoke_passed","user":"root","host":"vps-01","branch":"main","sha":"a1b2c3d","image_registry":"baseproject","image_tag":"20260528-1115-a1b2c3d","app_host":"app.example.com","admin_host":"admin.example.com"}
```

## Infra lifecycle

- Start infra: `./deploy/scripts/infra-up.sh <staging|production>`
- Stop infra: `./deploy/scripts/infra-down.sh <staging|production>`
- Optional profiles:
  - `--profile monitoring` for Prometheus/Grafana/Uptime Kuma/exporters
  - `--profile observability` for OpenSearch + Dashboards

## Monitoring defaults included

- Grafana dashboard (provisioned):
  - `BaseProject Infra Overview` (host CPU/RAM, container CPU/memory, API health/status)
- Grafana alert rules (provisioned):
  - `ServiceDown`
  - `HighHostCpu`
  - `ApiHealthProbeFailed`
  - `ApiHttp5xxDetected`

## Common failures (and quick fixes)

- `Missing deploy/env/shared/app.common.env`:
  - Copy from template: `cp deploy/env/shared/app.common.env.example deploy/env/shared/app.common.env`
- `Missing deploy/env/<env>/app.env` or `infra.env`:
  - Copy from matching `.env.example` in `deploy/env/<env>/`
- Api refuses startup with `AllowedHosts must be set...`:
  - Set `APP_HOST`, `ADMIN_HOST`, and `ALLOWED_HOSTS` consistently in `app.common.env`
- `--profile` not taking effect:
  - Pass profile flags to `infra-up.sh` exactly as shown above (before any custom `up` flags)

## Local dependencies

Developer laptops still use [`local/docker-compose.yml`](../local/docker-compose.yml) (not this stack).
