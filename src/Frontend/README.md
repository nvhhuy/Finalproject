# Frontend (Next.js base)

Reusable **frontend base** aligned with the backend in `src/Backend`.

**Documentation:** [`docs/frontend/`](../../docs/frontend/README.md) — onboarding, architecture, feature catalog, extensibility, AI rules.

## Features

- SEO-ready public pages (Next.js App Router)
- **i18n:** English, Tiếng Việt, 中文 (`react-i18next`)
- Auth: login, register, refresh, 2FA, Google sign-in, forgot/reset password, verify/resend email
- Profile: view + edit (`rowVersion`), resend verification, avatar upload demo
- 2FA: begin + confirm + QR code
- Media lab: presigned upload flow
- Typed API client + dev HTTPS proxy (`app/api/[...path]`)

## Env

Same as **Admin**: the browser calls `/api/*` on the dev server; Next proxies to **`https://localhost:7124`** (self-signed cert; `npm run dev` sets `NODE_TLS_REJECT_UNAUTHORIZED=0` for that hop only).

**Backend (HTTPS profile):**

```bash
cd src/Backend/Api
dotnet run --launch-profile https
```

Swagger: `https://localhost:7124/swagger`

## Run

```bash
npm install
npm run dev
```

Open:

- `/` landing
- `/login`
- `/app`

## Code structure

```
app/                 # Next.js routes only (pages, layouts, API proxy)
components/
  ui/                # Button, Input — no business logic
  layout/            # PublicHeader, LanguageSwitcher
  auth/              # GoogleSignIn, QrCode
  i18n/              # LocaleSync
lib/
  api/               # http client, ApiResponse types, idempotency
  auth/              # AuthProvider, tokens, RequireAuth
  features/          # Domain API modules (auth, users, media)
  shared/            # Reusable helpers — format date, display, pagination
  errors.ts          # ApiClientError
  ui/cn.ts           # className merge
i18n/                # en / vi / zh locales + config
```

**Use shared helpers** instead of inline formatting:

```ts
import { formatDateTimeOrDash, formatBytes, formatCount } from "@/lib/shared";
```

Add new cross-cutting utilities under `lib/shared/` (mirror Admin `shared/lib/`).
