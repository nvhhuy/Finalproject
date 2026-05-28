# Deploy checklist

Use this for both Option 1 and Option 2.

## Pre-deploy

- [ ] Release version is fixed (tag/SHA/image tags)
- [ ] Production env files are present from secret manager
- [ ] Migration script/job is ready
- [ ] Previous image tags are available for rollback
- [ ] Traefik routes for app/admin/api are correct
- [ ] If `monitoring` / `observability` profiles enabled: `OBSERVABILITY_BASIC_AUTH` set in `app.common.env` (htpasswd, `$` escaped as `$$`)

## Deploy sequence

1. Run DB migration.
2. Deploy Api service(s).
3. Wait for readiness (`/health/ready`).
4. Deploy Worker service.
5. Deploy Frontend/Admin.
6. Run smoke gate (`deploy/scripts/smoke.sh`).

Suggested commands:

```bash
./deploy/scripts/migrate.sh production
IMAGE_TAG=vX.Y.Z ./deploy/scripts/up.sh production
./deploy/scripts/smoke.sh production
docker compose --env-file deploy/env/shared/app.common.env --env-file deploy/env/production/app.env -f deploy/compose/stack.yml exec api curl -fsS http://127.0.0.1:8080/health/ready
```

## Post-deploy validation

- [ ] `GET /health/live` is healthy
- [ ] `GET /health/ready` is healthy
- [ ] Frontend login and key authenticated flow works
- [ ] Admin login and one protected action works
- [ ] Worker background processing is active (if enabled)
- [ ] Grafana is reachable and receiving metrics
- [ ] Uptime monitor reports services as up
- [ ] Log UI (OpenSearch Dashboards, if enabled) shows fresh logs
- [ ] Grafana alerts are loaded (`ServiceDown`, `HighHostCpu`, `ApiHealthProbeFailed`, `ApiHttp5xxDetected`)
- [ ] Deploy manifest appended in `deploy/releases/<env>/deploy.log`

## Rollback triggers

- Readiness fails repeatedly
- Critical auth flow regression
- Data migration side effects require immediate revert

Rollback should restore the previous image set and keep migration safety constraints in mind.

Rollback command example:

```bash
IMAGE_TAG=<previous-tag> ./deploy/scripts/up.sh production
./deploy/scripts/smoke.sh production
```
