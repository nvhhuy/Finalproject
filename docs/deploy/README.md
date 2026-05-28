# Deploy playbook (Docker runtime)

This folder documents production deployment choices for this repo.

**Repo-wide context:** [`../OVERVIEW.md`](../OVERVIEW.md) — honest production/security summary and service diagram.

Both supported options run the application on Docker and use Traefik as the edge reverse proxy.

## Options at a glance

| Option | CI/CD level | Runtime | Best fit |
|---|---|---|---|
| Option 1 — automated | Jenkins + Harbor (+ optional Sonar gate) | Docker Compose + Traefik | Frequent releases, multi-customer hosting, repeatable rollback |
| Option 2 — manual operator flow | SSH/VPN + scripted commands | Docker Compose + Traefik | Smaller projects, lower release frequency, single operator |

The two options differ in delivery workflow, not runtime architecture.

## Repo assets (code)

Implementation lives under [`deploy/`](../../deploy/):

- Dockerfiles: `deploy/docker/*/Dockerfile` → build from `src/Backend`, `src/Frontend`, `src/Admin`
- Compose: `deploy/compose/stack.yml`, `deploy/compose/traefik.yml`
- Env templates: `deploy/env/**`
- Scripts: `deploy/scripts/*.sh` — see [`deploy/README.md`](../../deploy/README.md)

## Required reading

- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — network and service topology
- [`OPTIONS.md`](./OPTIONS.md) — exact flow for Option 1 and Option 2
- [`ENVIRONMENT_STRATEGY.md`](./ENVIRONMENT_STRATEGY.md) — env file split and secret handling
- [`CHECKLIST.md`](./CHECKLIST.md) — pre/post deploy checks

## Non-negotiable deployment rules

1. Keep same-origin routing for browser clients (`/api` via Traefik), unless a project explicitly requires a public API domain.
2. Run database migration before rolling out new Api/Worker containers.
3. Keep `DataProtection` key ring persistent and shared by all Api replicas and Worker.
4. Never deploy with real secrets committed in git.
5. Keep Api and Worker as separate services; Worker is private-only and not publicly exposed.
