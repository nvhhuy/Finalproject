# Local development stack

Infrastructure only (no API container): **PostgreSQL**, **Redis**, **MinIO**, **OpenSearch** (+ Dashboards) so you can run the **Api** and **Worker** from your IDE with hot reload.

## Quick start

From repository root:

```bash
cd local
cp .env.example .env
docker compose up -d
```

On each `up`, two one-shot jobs run after Postgres is healthy (same pattern as `minio-init`):

| Service | What it does |
|---------|----------------|
| **db-migrate** | Applies pending EF Core migrations (`deploy/docker/migrate/Dockerfile`) |
| **postgres-quartz-init** | Creates `qrtz_*` tables if missing (for Worker persistent Quartz) |

First run may take 1–2 minutes while `db-migrate` builds the .NET SDK image; later runs are fast. Check logs: `docker compose logs db-migrate postgres-quartz-init`.

Then point `src/Backend/Api/appsettings.Development.json` (or user-secrets) at:

- Postgres **write** and **read** URLs — **the same single Postgres container is correct** for local dev. Two connection strings exist so you can later point **read** at a replica host without code changes.
- Redis connection string (SignalR backplane + cache when you enable `Realtime:Enabled`).
- MinIO S3-compatible URL and keys from `.env.example`.
- OpenSearch at `http://localhost:9200` (already referenced in `Api/appsettings.Development.json` Serilog sink). Apply the **5-day index retention** policy from [`opensearch/README.md`](./opensearch/README.md).

Stop:

```bash
docker compose down
```

Data volumes persist between restarts unless you `docker compose down -v`.

## Dev UIs (browser)

| Service | URL | Login |
|--------|-----|--------|
| **Postgres** (pgAdmin 4) | http://localhost:8080 | Email: `dev@local.dev`, Password: `admin` — server **BaseProject (local)** is pre-registered |
| **Redis** (Redis Commander) | http://localhost:8081 | (no auth — local only) |
| **RabbitMQ** (built-in) | http://localhost:15672 | `guest` / `guest` |
| **MinIO** console | http://localhost:9001 | `minio` / `minio123` |
| **MailHog** | http://localhost:8025 | — |
| **OpenSearch Dashboards** | http://localhost:5601 | security disabled in dev |

Override UI ports / pgAdmin login in `local/.env`: `PGADMIN_PORT`, `PGADMIN_EMAIL`, `PGADMIN_PASSWORD`, `REDIS_COMMANDER_PORT`.

If you change `POSTGRES_PASSWORD`, update `local/pgadmin/pgpass` to match (format `host:port:*:user:password`).

**ClamAV** has no web UI — check from the host with `nc -zv localhost 3310` or enable `MediaScanner` in the Worker and watch logs. First start downloads virus definitions (~250 MB); allow a few minutes.

## Connection strings (host machine → Docker)

| Service | From IDE / `appsettings.Development.json` |
|---------|---------------------------------------------|
| Postgres | `Host=localhost;Port=5432;Database=baseproject;Username=postgres;Password=postgres` |

> **Renamed from `finalproject`:** if you have an existing local volume with the old DB name, either keep using `POSTGRES_DB=finalproject` in `local/.env` or recreate: `docker compose down`, remove volume `local_pgdata`, then `up` again (data loss).
| Redis | `localhost:6379` |
| RabbitMQ | `amqp://guest:guest@localhost:5672` |
| MinIO (S3 API) | `http://localhost:9000` — keys in `.env.example` |
| OpenSearch | `http://localhost:9200` |
