# Deployment options

Both options deploy the same Docker runtime stack. The difference is automation depth.

## Option 1: Harbor-required pipeline (recommended default)

Suggested chain:

1. CI runs tests and build checks.
2. Build/publish images to Harbor using immutable `IMAGE_TAG` (semver or commit SHA).
3. Ensure infra stack is healthy (`infra-up` one-time, then monitor).
4. Run DB migration job.
5. Deploy Api (Compose recreate/update with explicit `IMAGE_TAG`; for true rolling, use orchestrator-level rollout).
6. Deploy Worker.
7. Deploy Frontend/Admin containers.
8. Keep monitoring profiles running (Prometheus/Grafana/Uptime) and alerting enabled.
9. Run smoke gate (`deploy/scripts/smoke.sh`) and fail the release if checks fail.

Typical tools:

- Jenkins for orchestration
- Harbor for image registry
- Sonar as optional quality gate

Use this option when:

- Releases happen at least weekly
- You need auditable, repeatable deploys
- Multiple environments/customers are hosted

## Option 2: No-Harbor manual flow (still Docker + Traefik)

Suggested chain:

1. Operator connects through SSH/VPN.
2. Ensure infra stack is running (`./deploy/scripts/infra-up.sh <env>`).
3. Checkout fixed git SHA/tag, then build images locally.
4. Apply local tag policy (for example `baseproject/<svc>:YYYYMMDD-HHMM-<sha>`).
5. Run DB migration.
6. Restart/recreate services with Docker Compose using local tags.
7. Run smoke gate (`deploy/scripts/smoke.sh <env>`).
8. Check Grafana/Uptime dashboards after each deploy.
9. Record deploy manifest (timestamp + SHA + local tags) for rollback.
   - This repo appends manifest automatically when `smoke.sh` passes.

Use this option when:

- Small deployment frequency
- Single operator workflow
- Lower operational overhead is preferred over full CI/CD automation

## Common rules for both options

- Do not run production from `local/docker-compose.yml`.
- Infra and app stacks are split (`deploy/compose/infra.yml` and `deploy/compose/stack.yml`).
- Keep release versions immutable (`IMAGE_TAG` for Option 1, deterministic local tags for Option 2).
- Always separate Api and Worker lifecycle.
- Keep rollback path documented (previous image tags).
