# Frontend onboarding (new developers)

Helps new developers **run the Frontend locally in ~15 minutes** and know **what to read next**. This is the **end-user** app (register, profile, media) — separate from the Admin Console.

---

## Where am I in the repo?

```
BaseProject/
├── src/Frontend/           ← Next.js app (this file)
├── src/Backend/Api/        ← API
├── src/Admin/              ← Admin SPA (operators)
├── local/docker-compose.yml
└── docs/frontend/          ← documentation you are reading
```

---

## Recommended reading path

| Order | Document | Time | Goal |
|-------|----------|------|------|
| 1 | **This file** | 15 min | Run locally + sign in |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 15 min | Existing routes + APIs |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | 20 min | Folders, auth, proxy |
| 4 | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) | 10 min | `formatDate`, `@/lib/shared` |
| 5 | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) | When adding features | Extension workflow |
| 6 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | When using AI | Frontend coding rules |

**You do not need to read everything at once.** Day 1: items 1–4; when changing code: add 5–6.

---

## Day 1 — checklist

### 1. Install prerequisites

- [Node.js ≥ 20.19](https://nodejs.org/) (required by Next.js 15)
- Backend API + Postgres (see [`docs/backend/ONBOARDING.md`](../backend/ONBOARDING.md))
- Docker: `docker compose -f local/docker-compose.yml up -d postgres` (from repo root)

### 2. Run Backend (HTTPS)

```bash
cd src/Backend/Api
dotnet run --launch-profile https
```

Swagger: `https://localhost:7124/swagger`

### 3. Configure Frontend

```bash
cd src/Frontend
cp .env.example .env.local   # optional
npm install
```

| Variable | Purpose |
|----------|---------|
| `API_PROXY_TARGET` | Server proxy → API (default `https://localhost:7124`) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Google Sign-In (optional) |
| `NEXT_PUBLIC_API_BASE_URL` | Production: call API directly (dev: leave empty = proxy `/api`) |

### 4. Run dev server

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

The browser calls **`/api/v1/...`** on the same origin; the Next route `app/api/[...path]/route.ts` forwards to the backend (avoids CORS + self-signed cert issues).

### 5. Sign in (Development seed)

| Field | Value |
|-------|-------|
| Email | `admin@localhost.com` |
| Password | `Temp!ChangeMe123` |

The seeded user may have Admin role — it can still log in on the Frontend (unlike the Admin app, which requires Admin role only).

### 6. Quick smoke test

- [ ] `/` landing — switch language EN / VI / ZH
- [ ] `/login` → `/app` dashboard
- [ ] `/app/profile` — edit name, resend verification
- [ ] `/app/security/2fa` — begin setup + QR
- [ ] `/app/media` — upload file, poll status
- [ ] `/register`, `/forgot-password` (UI flows)

### 7. Typecheck

```bash
npm run typecheck
```

---

## Mental model

| Layer | Role |
|-------|------|
| `app/**/page.tsx` | Route UI — thin, composes components |
| `lib/features/*` | API calls by domain (auth, users, media) |
| `lib/api/http.ts` | Envelope, JWT, refresh, idempotency |
| `lib/auth/*` | Session, `RequireAuth`, hydrate profile |
| `lib/shared/*` | Format date, empty cells, pagination helpers |
| `i18n/locales/*.json` | All UI strings (en, vi, zh) |

**Do not** call `fetch` directly to `https://localhost:7124` from the browser in dev — use path `/api/v1/...`.

---

## Common issues

| Symptom | Fix |
|---------|-----|
| CORS / `Failed to fetch` | Backend not running; or remove `NEXT_PUBLIC_API_BASE_URL` from `.env.local` to use proxy |
| `Internal Server Error` on `/api` | Restart Frontend after pull; check `API_PROXY_TARGET` |
| Hydration `lang` mismatch | Switch language once (writes cookie); see `LocaleSync` |
| Login 401 | Wrong password; Postgres not seeded; try login via Swagger |

---

## Next steps

- Add a product screen: [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md)
- AI/Cursor: [`.cursor/rules/frontend.mdc`](../../.cursor/rules/frontend.mdc) + [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
