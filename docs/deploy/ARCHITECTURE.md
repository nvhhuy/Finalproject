# Runtime architecture (common to both options)

Both deployment options use the same runtime model:

- Docker Compose for workload orchestration
- Traefik for TLS termination and routing
- Api and Worker as separate services
- Browser clients calling same-origin `/api`
- Split stacks:
  - `deploy/compose/infra.yml` for shared infrastructure
  - `deploy/compose/stack.yml` for application services
  - optional infra profiles:
    - `monitoring` (Prometheus, Grafana, Uptime Kuma, exporters)
    - `observability` (OpenSearch, Dashboards)

## Service map

```text
Internet
  -> Traefik (public edge)
       -> Frontend service (app.example.com)
       -> Admin service (admin.example.com)
       -> Api service via /api path routing
Private network
  -> Worker service (no public route)
  -> Postgres / Redis / RabbitMQ / MinIO / ClamAV
  -> Optional observability: OpenSearch + Dashboards
  -> Optional monitoring: Prometheus + exporters + Grafana + Uptime Kuma
```

## Routing conventions

- `https://app.<domain>/api/*` -> Api container
- `https://admin.<domain>/api/*` -> Api container
- Worker has no public ingress route.

Traefik route definitions must stay in versioned compose files to keep deployments repeatable.

## Security baseline

- TLS on all public hosts via Traefik.
- Api must run with explicit `AllowedHosts` in production.
- Set `ReverseProxy__KnownProxies` and/or `ReverseProxy__KnownNetworks` for the edge proxy.
- Keep service-to-service traffic on private Docker network.
- **Observability UIs** (Grafana, OpenSearch Dashboards, Uptime Kuma) must use Traefik basic auth via `OBSERVABILITY_BASIC_AUTH` in `app.common.env` when `monitoring` / `observability` profiles are enabled. OpenSearch runs with security plugin disabled internally — do not expose it without the edge auth layer.
- **Prometheus** stays on the private `app` network only (no public Traefik route).
- Post-deploy smoke (`deploy/scripts/smoke.sh`) fail-fast checks frontend/admin HTTP 200 and API routing (expects HTTP 400 on empty login POST, not 5xx).

## Rollout baseline

- Use rolling replacement for Api replicas where possible.
- Deploy Worker after Api rollout (or in a controlled second step).
- Health probes:
  - liveness: `/health/live`
  - readiness: `/health/ready`

For replica counts, env prerequisites (Redis backplane, Quartz store), and tier-by-tier growth, see [`SCALING.md`](./SCALING.md).
