# Admin onboarding (new developers)

Helps new developers **run Admin locally in ~15 minutes** and know **what to read next**. Admin is an internal SPA — it does not replace Swagger, but it is the day-to-day operations tool (users, audit, DLQ, settings).

---

## Where am I in the repo?

```
BaseProject/
├── src/Admin/              ← Admin Console (Vite + React)
├── src/Backend/Api/        ← API that Admin calls
├── local/docker-compose.yml
└── docs/admin/             ← documentation you are reading
```

---

## Recommended reading path

| Order | Document | Time | Goal |
|-------|----------|------|------|
| 1 | **This file** | 15 min | Run locally + sign in |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 15 min | Know existing screens / APIs |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | 20 min | Folders, auth, data flow |
| 4 | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) | 15 min | Shared helpers (date, toast, …) |
| 5 | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) | When adding features | Extension workflow |
| 6 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | When using AI | Admin coding rules |

**You do not need to read everything at once.** Day 1: items 1–3; when changing code: add 4–6.

---

## Day 1 — checklist

### 1. Install prerequisites

- [Node.js ≥ 20.19](https://nodejs.org/) (matches `package.json` engines)
- Backend API running (see [`docs/backend/ONBOARDING.md`](../backend/ONBOARDING.md))
- Local Docker (Postgres, …): `docker compose -f local/docker-compose.yml up -d` from repo root

### 2. Configure Admin

```bash
cd src/Admin
cp .env.example .env   # optional — adjust proxy if API port differs
npm install
```

| Variable | Purpose |
|----------|---------|
| `VITE_API_PROXY_TARGET` | Target for Vite proxy `/api` → backend (default `https://localhost:7124`) |
| `VITE_API_BASE_URL` | Production: absolute API origin (leave empty = same-origin `/api`) |

### 3. Run dev server

```bash
npm run dev
```

Open [http://localhost:5174](http://localhost:5174).

Backend `Cors:AllowedOrigins` must include `http://localhost:5174` (already in `appsettings.Development.json`).

### 4. Sign in (Development)

| Field | Value |
|-------|-------|
| Email | `admin@localhost.com` |
| Password | `Temp!ChangeMe123` |

Only accounts with **Admin** role can complete the login flow. If 2FA is enabled on the admin user, follow the verification screen.

### 5. Quick smoke test

- [ ] Dashboard loads metrics (or error alert if API is down)
- [ ] Users — list, filter, open user detail
- [ ] Switch language (header / sidebar / login) — EN / VI / ZH
- [ ] Settings — toggle public registration
- [ ] Audit logs — filter by `targetUserId` from user detail

### 6. Build & lint

```bash
npm run build
npm run lint
```

---

## Source structure (summary)

```
src/Admin/src/
├── app/              # App, router, providers (Query, auth hydrate, Toaster)
├── config/           # env (API_V1)
├── features/         # Screens by domain
│   ├── auth/
│   ├── dashboard/
│   ├── users/
│   ├── settings/
│   ├── audit/
│   └── messaging/    # DLQ
├── i18n/             # en, vi, zh + config
└── shared/
    ├── api/          # client, admin-api, auth-api
    ├── components/   # ui, layout, patterns
    ├── constants/    # query-keys, styles
    ├── hooks/
    ├── lib/          # format, validation, toast, …
    ├── stores/
    └── types/        # DTO mirror backend
```

Details: [`ARCHITECTURE.md`](./ARCHITECTURE.md).

---

## Daily conventions

1. **New feature** → read [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) before coding.
2. **UI strings** → add keys to **all 3** files `i18n/locales/{en,vi,zh}.json`.
3. **Dates/times** → use `@/shared/lib` (`formatDateOrDash`, …), do not call `toLocaleDateString()` directly in pages.
4. **API mutations** → `idempotent: true` via `admin-api`; user update/delete requires `rowVersion` (see backend concurrency doc).
5. **Query cache** → use `queryKeys` from `@/shared/constants/query-keys`.

---

## Troubleshooting

| Symptom | Suggestion |
|---------|------------|
| CORS error | Add Admin origin to backend `Cors:AllowedOrigins` |
| 401 after a few minutes | Refresh token in sessionStorage; check API `/auth/refresh` |
| SSL proxy error | Try `VITE_API_PROXY_TARGET=http://localhost:5076` if API is HTTP |
| Metrics / list empty | API not running or DB not seeded |
| Concurrency conflict toast | User edited concurrently — refresh page, get new `rowVersion` |

---

## Next steps

- Add a screen: [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md)
- Feature list: [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md)
- Code with AI: [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) + rule `.cursor/rules/admin.mdc`
