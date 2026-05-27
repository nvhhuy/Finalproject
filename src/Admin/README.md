# Admin Console

Internal administrator UI for the Final Project backend. Built with **Vite + React 19 + TypeScript**.

**Full documentation:** [`docs/admin/`](../../docs/admin/README.md) — onboarding, features, architecture, extensibility, AI rules.

## Scope

- **Included:** sign-in (with 2FA), dashboard metrics, user management, settings, audit logs, DLQ tooling, UI i18n (English, Tiếng Việt, 中文).
- **Excluded:** public registration and end-user flows (separate frontend project).

## Prerequisites

- Node.js ≥ 20.19
- Backend API running (see `src/Backend/Api`)
- Local stack: `docker compose -f local/docker-compose.yml up -d` from repo root

Default seeded admin (Development):

- Email: `admin@localhost`
- Password: `Temp!ChangeMe123`

## Development

```bash
cd src/Admin
npm install
npm run dev
```

Open [http://localhost:5174](http://localhost:5174). API calls are proxied to `https://localhost:7124` (override via `.env` → `VITE_API_PROXY_TARGET`).

Ensure backend `Cors:AllowedOrigins` includes `http://localhost:5174` (already set in `appsettings.Development.json`).

## Production build

```bash
npm run build
npm run preview
```

Set `VITE_API_BASE_URL` to your public API origin when not using a reverse-proxy `/api` path.

## Security notes

- Access tokens are kept **in memory**; refresh tokens use **sessionStorage** (cleared when the tab closes).
- Only principals with JWT role **Admin** can complete sign-in.
- Mutating admin requests send `Idempotency-Key` automatically.
- Impersonation tokens are shown once in a modal — copy for debugging only.

## Project structure

```
src/
  app/              # Router, providers
  config/           # Environment
  features/         # Feature modules (auth, users, …)
  i18n/             # Locales (en, vi, zh) + i18next config
  shared/
    api/            # Typed API client
    components/     # UI, layout, patterns
    constants/      # query-keys, shared CSS class strings
    hooks/          # useDebouncedValue, usePagination
    lib/            # format, display, validation, toast, …
    stores/         # Auth state (Zustand)
    types/          # API DTO mirrors
```

## Docs quick links

| Topic | Doc |
|-------|-----|
| Onboarding | [`docs/admin/ONBOARDING.md`](../../docs/admin/ONBOARDING.md) |
| Features | [`docs/admin/FEATURE_CATALOG.md`](../../docs/admin/FEATURE_CATALOG.md) |
| Add a feature | [`docs/admin/EXTENSIBILITY_GUIDE.md`](../../docs/admin/EXTENSIBILITY_GUIDE.md) |
| Shared helpers | [`docs/admin/COMMON_LIBRARIES.md`](../../docs/admin/COMMON_LIBRARIES.md) |
| AI / Cursor rules | [`docs/admin/LLM_FEATURE_IMPLEMENTATION_RULES.md`](../../docs/admin/LLM_FEATURE_IMPLEMENTATION_RULES.md) |
