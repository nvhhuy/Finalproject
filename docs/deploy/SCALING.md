# Scaling guide

How to grow this stack from a single VPS to higher traffic and more tenants. This document is **operator-focused** (Docker Compose + Traefik). Application-level limits and code gaps are in [`../backend/PRODUCTION_READINESS_REPORT.md`](../backend/PRODUCTION_READINESS_REPORT.md).

**Related:** [`ARCHITECTURE.md`](./ARCHITECTURE.md) · [`CHECKLIST.md`](./CHECKLIST.md) · [`ENVIRONMENT_STRATEGY.md`](./ENVIRONMENT_STRATEGY.md) · [`../../deploy/README.md`](../../deploy/README.md)

---

## What “scale” means here

| Action | Same VPS, more capacity | New VPS / managed services |
|--------|-------------------------|----------------------------|
| Deploy new app version | `migrate.sh` → `up.sh` | Same |
| Add Api replicas | ✅ `--scale api=N` | ✅ multiple app hosts behind LB |
| Add Worker replicas | ✅ `--scale worker=N` | ✅ |
| Bigger Postgres | Tune RAM / disk on host | Move to managed Postgres or dedicated DB VM |
| Read-heavy traffic | Point read connection string to replica | Managed read replica |
| Object storage growth | Expand MinIO volume | S3 / R2 / B2 (change env only) |

**Moving to a new server** (backup → restore → DNS) is a **migration**, not horizontal scale. Infra is long-lived; you do not re-run full first-time setup on every release. See [§ Moving vs scaling](#moving-vs-scaling).

---

## Scale tiers (practical)

### Tier 0 — Single VPS (~8GB RAM)

**Fit:** launch, staging, low traffic, few tenants (single-tenant or pilot).

```text
Traefik → Api×1, Worker×1, Frontend, Admin
          Postgres, Redis, RabbitMQ, MinIO (same host)
```

**Recommendations:**

- Run `./deploy/scripts/infra-up.sh production` once; keep volumes.
- Enable Redis + RabbitMQ in `app.env` (see [`deploy/env/production/app.env.example`](../../deploy/env/production/app.env.example)).
- Skip heavy profiles on small RAM: `--profile monitoring` and `--profile observability` optional.
- ClamAV is RAM-heavy; defer or scan async if the host is tight.
- Use automated DB/settings backup jobs (Admin → Settings) and off-site object storage.

### Tier 1 — Same stack, more headroom (~16–32GB)

**Fit:** more concurrent users, heavier media, stable production.

1. **Vertical:** upgrade VPS RAM/CPU and Postgres `shared_buffers` / connection limits.
2. **Horizontal Api:** 2–4 Api containers behind Traefik (see [§ Scale Api horizontally](#scale-api-horizontally)).
3. **Horizontal Worker:** 2+ Worker containers with Quartz persistent store (already default).
4. **Tune:** PgBouncer in front of Postgres if connection count grows.

No change to release flow: still `migrate.sh` → `up.sh` → `smoke.sh`.

### Tier 2 — Split data plane

**Fit:** Postgres or disk becomes the bottleneck; need HA-ish DB and durable media.

| Component | Move to |
|-----------|---------|
| Postgres | Managed DB (RDS, Neon, Supabase, etc.) or dedicated DB VM |
| Media | S3-compatible managed bucket (update `OBJECT_STORAGE_*` in `app.env`) |
| Redis | Managed Redis or dedicated small VM |
| App tier | Same or larger VPS running only `stack.yml` services |

Update connection strings in vault/`app.env` only — no app code change for read replica hook:

```bash
DATABASE_CONNECTION_STRING=Host=<primary>;...
DATABASE_READ_CONNECTION_STRING=Host=<replica>;...
```

### Tier 3 — Large scale / multi-tenant SaaS

**Fit:** many tenants, high QPS, strict SLA.

Requires **infrastructure outside this Compose template** (K8s, multiple app regions, OpenSearch cluster, tenant isolation in code). The repo provides **hooks**, not turnkey cluster manifests:

- Api/Worker split, outbox, RabbitMQ, Redis SignalR backplane
- Read/write DbContext
- Feature-flag canary with optional `TenantId` (tenant model not fully implemented — see [`../backend/EXTENSIBILITY_AND_GAPS.md`](../backend/EXTENSIBILITY_AND_GAPS.md))

Plan separately: `ITenantContext`, row-level `TenantId`, search index pipeline, notification fan-out service.

---

## Architecture already prepared for scale

| Capability | Config / location | Required when |
|------------|-------------------|---------------|
| Api / Worker split | `deploy/compose/stack.yml` | Always |
| Health probes | `/health/live`, `/health/ready` | Rolling deploy, monitoring |
| Traefik load balancing | Docker labels on `api` service | Api replicas > 1 |
| SignalR Redis backplane | `REALTIME_USE_REDIS_BACKPLANE=true` | **Api replicas > 1** |
| Read replica queries | `DATABASE_READ_CONNECTION_STRING` | Read-heavy lists/search |
| Async messaging | `RABBITMQ_ENABLED=true`, outbox | Background work, email queue |
| Multi Worker cron | `WORKER_QUARTZ_USE_PERSISTENT_STORE=true` | **Worker replicas > 1** |
| Shared DataProtection keys | volume `dataprotection_keys` | **All Api + Worker replicas** |
| Object storage | MinIO or external S3 | Media at scale |
| Metrics / alerts | `infra-up --profile monitoring` | Production ops |

---

## Scale Api horizontally

### Prerequisites

Set in `deploy/env/<env>/app.env`:

```bash
REALTIME_ENABLED=true
REALTIME_USE_REDIS_BACKPLANE=true
REDIS_CONNECTION_STRING=redis:6379,password=...,abortConnect=false
```

Without Redis backplane, SignalR connections break when Traefik routes users to different Api instances.

### Deploy multiple Api containers

After a normal migration:

```bash
./deploy/scripts/migrate.sh production
IMAGE_TAG=vX.Y.Z ./deploy/scripts/up.sh production --scale api=2
./deploy/scripts/smoke.sh production
```

Traefik discovers all `api` containers on the `proxy` network and load-balances by default.

### Verify

```bash
# Internal readiness (any replica)
docker compose --env-file deploy/env/shared/app.common.env \
  --env-file deploy/env/production/app.env \
  -f deploy/compose/stack.yml exec api curl -fsS http://127.0.0.1:8080/health/ready

# All replicas share DataProtection volume
docker compose ... -f deploy/compose/stack.yml ps api
```

### Rollout note

Prefer **migrate before scale-up** so all replicas start on the same schema. Roll back image tag with `IMAGE_TAG=<previous> ./deploy/scripts/up.sh production --scale api=2` per [`CHECKLIST.md`](./CHECKLIST.md).

---

## Scale Worker horizontally

### Prerequisites

```bash
WORKER_QUARTZ_USE_PERSISTENT_STORE=true
RABBITMQ_ENABLED=true
```

Quartz persistent store prevents duplicate cron execution across Worker pods. Outbox + RabbitMQ distributes async work.

### Deploy

```bash
IMAGE_TAG=vX.Y.Z ./deploy/scripts/up.sh production --scale worker=2
```

Worker has **no public Traefik route** — only internal network access.

Monitor RabbitMQ queue depth and Worker logs when adding replicas. If one job type dominates, split queues in application code later (see [`../backend/MESSAGING_AND_WORKER.md`](../backend/MESSAGING_AND_WORKER.md)).

---

## Database scaling

### Single Postgres (default Compose)

`deploy/compose/infra.yml` runs one Postgres instance. Suitable through Tier 1 if tuned and backed up.

**Before pain appears:**

- Index list endpoints (admin user search uses SQL `Contains` — slow at large row counts).
- Connection pooling (PgBouncer sidecar or managed pooler).
- Regular backups (worker job + verify restore).

### Read replica

Application already uses a separate read DbContext. Point env at replica:

```bash
DATABASE_CONNECTION_STRING=Host=postgres-primary;...
DATABASE_READ_CONNECTION_STRING=Host=postgres-replica;...
```

No redeploy of infra stack required if replica is reachable from the app network. Slight replication lag is acceptable for list/read endpoints; writes always use primary.

### Managed Postgres

Replace `Host=postgres` with provider hostname in both connection strings. Keep `Database__ApplyPendingMigrations: false` in production; run [`migrate.sh`](../../deploy/scripts/migrate.sh) from CI or operator shell.

---

## Redis, RabbitMQ, and object storage

| Service | Scale approach |
|---------|----------------|
| **Redis** | Single instance in Compose is enough for cache + SignalR backplane through Tier 1–2. High availability → managed Redis with same connection string format. |
| **RabbitMQ** | Single broker in Compose; scale **consumers** (Worker replicas). Clustered RabbitMQ is out of scope for default deploy. |
| **MinIO** | Grow disk volume or migrate to external S3 (`OBJECT_STORAGE_SERVICE_URL`, keys, bucket in `app.env`). |
| **ClamAV** | CPU/RAM heavy; scale by async scan queue or dedicated scan Worker later. |

---

## Frontend and Admin

- **Frontend (Next.js):** usually 1 container suffices; static assets can move to CDN without architecture change.
- **Admin (nginx static):** 1 container; negligible scale concern.

Bottleneck is almost always **Api + Postgres + media**, not FE/Admin containers.

---

## Observability at scale

Enable when operators need visibility (not required for Tier 0):

```bash
./deploy/scripts/infra-up.sh production --profile monitoring
# optional logs:
./deploy/scripts/infra-up.sh production --profile observability
```

Set `OBSERVABILITY_BASIC_AUTH` in `app.common.env` before exposing Grafana / OpenSearch Dashboards. Prometheus stays on the private `app` network.

Watch Grafana alerts: `ServiceDown`, `ApiHealthProbeFailed`, `HighHostCpu`, `ApiHttp5xxDetected`.

---

## Environment checklist by scale step

### Before Api×2

- [ ] `REALTIME_USE_REDIS_BACKPLANE=true`
- [ ] Redis reachable from all Api containers
- [ ] `dataprotection_keys` volume mounted on every Api replica (default in `stack.yml`)
- [ ] `ReverseProxy__KnownNetworks` matches Traefik/Docker network

### Before Worker×2

- [ ] `WORKER_QUARTZ_USE_PERSISTENT_STORE=true`
- [ ] `RABBITMQ_ENABLED=true`
- [ ] Same `dataprotection_keys` volume on Worker

### Before read replica

- [ ] `DATABASE_READ_CONNECTION_STRING` points to replica
- [ ] Replica user has read-only grants
- [ ] Monitoring replication lag

### Before managed DB / S3

- [ ] Secrets updated in vault, not git
- [ ] Smoke test + one authenticated flow after cutover
- [ ] Backup/restore tested on new target

---

## Moving vs scaling

| Scenario | Backup DB? | Re-run `infra-up`? | App deploy |
|----------|--------------|--------------------|------------|
| New release, same server | Optional (scheduled backups) | No | `migrate` → `up` |
| Scale Api/Worker on same server | No | No | `up --scale ...` |
| New VPS / provider migration | **Yes** (+ media, DataProtection keys) | **Yes** on new host | restore → `migrate` → `up` |
| Managed Postgres cutover | Provider snapshot | No (change env only) | `up` after connection string change |

**Data to preserve on migration:**

| Asset | Why |
|-------|-----|
| Postgres | Users, tickets, settings |
| Object storage bucket | Uploads |
| `dataprotection_keys` volume | Encrypted cookies/tokens |
| Env secrets | JWT, DB passwords |

Redis and in-flight RabbitMQ messages are usually rebuildable; outbox rows in Postgres allow retry after brief outage.

---

## Known limits (honest)

Not solved by Compose scale alone:

- **Multi-tenant SaaS** — needs `TenantId` + query filters ([`../backend/EXTENSIBILITY_AND_GAPS.md`](../backend/EXTENSIBILITY_AND_GAPS.md)).
- **Large admin user search** — needs OpenSearch/FTS index pipeline.
- **Mass notification fan-out** — SignalR + Redis helps; very large audiences need dedicated notification delivery.
- **Autoscaling** — no HPA in repo; use orchestrator or manual replica count.
- **Postgres sharding** — not included; Tier 3+ planning.

For code-level readiness detail, see [`../backend/PRODUCTION_READINESS_REPORT.md`](../backend/PRODUCTION_READINESS_REPORT.md) § Scalability checklist.

---

## Quick command reference

```bash
# First-time (once per environment)
./deploy/scripts/traefik-up.sh
./deploy/scripts/infra-up.sh production
./deploy/scripts/migrate.sh production
./deploy/scripts/up.sh production

# Scale Api + Worker (example)
IMAGE_TAG=v1.2.3 ./deploy/scripts/up.sh production --scale api=2 --scale worker=2
./deploy/scripts/smoke.sh production

# Release only (infra unchanged)
./deploy/scripts/migrate.sh production
IMAGE_TAG=v1.2.3 ./deploy/scripts/up.sh production
./deploy/scripts/smoke.sh production
```

---

## Summary

| Question | Answer |
|----------|--------|
| Does infra support scaling? | **Yes** for Tier 0–2 via Compose: Api/Worker replicas, read replica env, external DB/S3. |
| Is large-scale SaaS turnkey? | **No** — needs managed clusters + multi-tenant/search/notification work. |
| Every deploy re-setup infra? | **No** — only first boot or server migration. |
| First step when traffic grows? | Vertical RAM → Api×2 + Redis backplane → read replica → managed DB/S3. |
