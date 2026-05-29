# Tenant hosting (subdomains & custom domains)

## Subdomain workspaces (`{slug}.app.example.com`)

1. Set in `deploy/env/shared/app.common.env`:
   - `TENANT_SUBDOMAIN_BASE_HOST=app.example.com` (same as `APP_HOST` for MVP)
2. Traefik routes in `deploy/compose/stack.yml`:
   - `frontend-tenants` and `api-tenants` use `HostRegexp(\`{subdomain:[a-z0-9-]+}.app.example.com\`)`
3. API `Tenancy:SubdomainBaseHost` must match (wired via `Tenancy__SubdomainBaseHost` in stack).
4. Frontend `TENANT_SUBDOMAIN_BASE_HOST` enables Next.js middleware rewrite to `/t/[slug]`.

**TLS:** HTTP-01 ACME issues a cert per hostname. For many tenant subdomains use **DNS-01 wildcard** (`*.app.example.com`) in Traefik (`certificatesresolvers.le.dnschallenge`).

## Custom domains

1. Tenant owner adds domain in `/app/tenant` → `POST /api/v1/tenants/{id}/domains`.
2. DNS: TXT `_baseproject-verify.{hostname}` = token **or** CNAME to `Tenancy:PlatformCnameTarget`.
3. `POST .../domains/{id}/verify` checks TXT via Google Public DNS API.
4. Point customer DNS A/ALIAS/CNAME to Traefik; add a Traefik router for each hostname (file provider or automation).

Example file-provider snippet (`deploy/compose/tenant-domains.dynamic.yml.example`):

```yaml
http:
  routers:
    tenant-portal-acme:
      rule: Host(`portal.customer.com`)
      entryPoints: [websecure]
      tls:
        certResolver: le
      service: frontend
  services:
    frontend:
      loadBalancer:
        servers:
          - url: http://frontend:3000
```

Enable in Traefik: `--providers.file.directory=/dynamic` and mount verified hostnames.

## Local dev

```bash
# /etc/hosts
127.0.0.1 acme.localhost app.localhost
```

Set `Tenancy__SubdomainBaseHost=localhost` and `TENANT_SUBDOMAIN_BASE_HOST=localhost` for the frontend dev server.
