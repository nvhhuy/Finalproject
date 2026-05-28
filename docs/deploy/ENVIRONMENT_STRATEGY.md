# Environment file strategy

To keep deploys manageable, split environment files by environment and sensitivity.

## Goals

- Clear ownership and diff visibility
- No production secrets committed to git
- Predictable container startup configuration

## Recommended layout

```text
deploy/env/
  shared/
    app.common.env.example   → copy to app.common.env
    traefik.env.example      → copy to traefik.env
  staging/
    infra.env.example        → copy to infra.env
    app.env.example          → copy to app.env
  production/
    infra.env.example        → copy to infra.env
    app.env.example          → copy to app.env
```

Per customer/project, keep real secrets outside git:

- `deploy/env/production/*.env` in secret storage or encrypted vault
- Inject at deploy time (Jenkins credentials, Vault, SOPS, 1Password, etc.)

## Compose usage

Prefer explicit env file composition:

```bash
docker compose \
  --env-file deploy/env/shared/app.common.env \
  --env-file deploy/env/production/app.env \
  -f deploy/compose/stack.yml \
  up -d
```

Infra stack uses:

```bash
docker compose \
  --env-file deploy/env/shared/app.common.env \
  --env-file deploy/env/production/infra.env \
  -f deploy/compose/infra.yml \
  up -d
```

## Naming conventions

- Keep service-specific variables grouped by prefix where possible:
  - `API__*` (or ASP.NET style keys such as `JwtSettings__Secret`)
  - `WORKER__*` where service-specific
  - `TRAEFIK__*` for proxy settings
- Use one value source per key per environment to avoid hidden overrides.

## Secret handling policy

- Never commit real credentials to `.env` files.
- Commit only `.env.example` templates with safe placeholders.
- Rotate secrets when operator access changes.

## Runtime-critical keys checklist

- Database connection strings (write/read)
- JWT signing secret and issuer/audience
- DataProtection keys location
- RabbitMQ credentials (if enabled)
- Object storage credentials and private bucket settings
