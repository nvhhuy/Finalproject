# Local development stack

Infrastructure only (no API container): **PostgreSQL**, **Redis**, **MinIO**, **OpenSearch** (+ Dashboards) so you can run the **Api** and **Worker** from your IDE with hot reload.

## Quick start

From repository root:

```bash
cd local
cp .env.example .env
docker compose up -d
```

Then point `src/Backend/Api/appsettings.Development.json` (or user-secrets) at:

- Postgres **write** and **read** URLs — **the same single Postgres container is correct** for local dev. Two connection strings exist so you can later point **read** at a replica host without code changes.
- Redis connection string (SignalR backplane + cache when you enable `Realtime:Enabled`).
- MinIO S3-compatible URL and keys from `.env.example`.
- OpenSearch at `http://localhost:9200` (already referenced in `Api/appsettings.Development.json` Serilog sink). Apply the **5-day index retention** policy from [`opensearch/README.md`](./opensearch/README.md).

OpenSearch Dashboards: http://localhost:5601

Stop:

```bash
docker compose down
```

Data volumes persist between restarts unless you `docker compose down -v`.
