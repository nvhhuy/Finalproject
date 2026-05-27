# LLM / AI feature implementation rules (Frontend)

Strict guide for Cursor and other AI assistants when changing **`src/Frontend/`** only.

Human developers should follow the same rules where practical.

---

## Scope

- **In scope:** `src/Frontend/**`, `docs/frontend/**`
- **Out of scope:** `src/Backend/**` (unless user asks), `src/Admin/**` (use `docs/admin/`)
- **Docs:** update `docs/frontend/FEATURE_CATALOG.md` when adding user-visible routes or APIs

---

## Read first (context)

| Task | Document |
|------|----------|
| Onboarding / run locally | `docs/frontend/ONBOARDING.md` |
| What exists | `docs/frontend/FEATURE_CATALOG.md` |
| Folders & auth | `docs/frontend/ARCHITECTURE.md` |
| Helpers | `docs/frontend/COMMON_LIBRARIES.md` |
| How to add pages | `docs/frontend/EXTENSIBILITY_GUIDE.md` |

---

## Non-negotiable rules

1. **Layer boundaries**
   - `app/*`, `components/*` → may import `lib/*`, `i18n/*`
   - `lib/features/*` → may import `lib/api/*`, `lib/errors` — no imports from `app/*`
   - `lib/shared/*` → pure helpers; no imports from `app/*` or `components/*`

2. **i18n for all user-visible strings**
   - `useTranslation()` + `t("key")`
   - Add keys to **`en.json`, `vi.json`, and `zh.json` together**
   - Exception: raw API `error.message` from server

3. **Formatting**
   - Use `@/lib/shared` for dates, numbers, bytes, empty cells
   - Never add `toLocaleDateString()` / `toLocaleString()` in pages

4. **API access**
   - Extend `lib/features/<domain>/*-api.ts`
   - Use `apiRequest` / `apiRaw` from `lib/api/http.ts`
   - Browser dev: paths like `/api/v1/...` (proxied), not hard-coded `https://localhost:7124`
   - `idempotent: true` when backend requires `Idempotency-Key`
   - Include `rowVersion` when backend uses optimistic concurrency

5. **Auth**
   - Protected routes live under `app/app/*` with existing `RequireAuth` layout
   - Do not store tokens outside `lib/auth/tokens.ts` patterns
   - Login / 2FA: use `apiRaw` for HTTP 202 handling

6. **Minimize diff**
   - No unrelated refactors, dependency bumps, or formatting sweeps
   - Match existing Tailwind + `Button`/`Input` patterns

7. **TypeScript**
   - Avoid `any` without comment
   - Run `npm run typecheck` in `src/Frontend` after substantive changes

8. **SSR / hydration**
   - Do not read `localStorage` in Server Components
   - Locale: use cookie pattern in `app/layout.tsx`; client sync via `LocaleSync`
   - Do not hard-code `<html lang="vi">` — derive from cookie helper

---

## File placement

| What | Where |
|------|-------|
| New authenticated page | `app/app/<segment>/page.tsx` |
| New public page | `app/<segment>/page.tsx` |
| Public marketing only | `app/page.tsx` + `components/layout/PublicShell.tsx` |
| Domain API calls | `lib/features/<domain>/<domain>-api.ts` |
| Cross-cutting pure fn | `lib/shared/` + export in `index.ts` |
| Reusable hook (2+ uses) | `lib/hooks/` |
| UI primitive | `components/ui/` |
| Feature-specific component | `components/<domain>/` |
| Auth/session | `lib/auth/` only |
| API proxy | `app/api/[...path]/route.ts` (change only if necessary) |

---

## Page template (authenticated)

```tsx
"use client";

import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { formatDateTimeOrDash } from "@/lib/shared";

export default function ExamplePage() {
  const { t } = useTranslation();
  // useQuery + feature API
  return (
    <div className="grid gap-4">
      <h1 className="text-2xl font-semibold">{t("example.title")}</h1>
      {/* ... */}
    </div>
  );
}
```

---

## Forms

- Prefer Zod + `createAuthSchemas(t)` pattern or local schema with `t()` messages
- Map API errors via `ApiClientError` or `envelope.error`
- Concurrency: `isConcurrencyError` → `t("errors.concurrencyConflict")`

---

## Do not implement in Frontend (redirect)

| Request | Correct location |
|---------|------------------|
| Admin user list, impersonate, DLQ | `src/Admin` |
| New backend endpoint only | `src/Backend` first |
| SignalR hub client (unless explicitly requested) | Separate task + `Realtime:Enabled` |

---

## PR / handoff checklist

- [ ] Routes listed in `FEATURE_CATALOG.md` if user-facing
- [ ] i18n: en + vi + zh
- [ ] No direct backend URL in client-side `fetch` for dev
- [ ] `npm run typecheck` passes
- [ ] README or EXTENSIBILITY unchanged unless workflow changed

---

## Cursor rule file

Repo rule: [`.cursor/rules/frontend.mdc`](../../.cursor/rules/frontend.mdc) (auto-attached for `src/Frontend/**`).
