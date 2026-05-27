# Frontend extensibility guide

Step-by-step workflow for adding product features on top of the base without breaking conventions.

---

## Before you start

1. Confirm the **backend endpoint** exists (or implement in Backend first — [`docs/backend/BASE_GUIDE.md`](../backend/BASE_GUIDE.md)).
2. Read [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) to avoid duplicating routes.
3. Reuse [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md).

4. **Scope:** end-user features only — admin operations belong in `src/Admin`.

---

## Decision tree

```
New capability?
├── Landing/marketing UI only → app/page.tsx + i18n
├── API exists, add authenticated screen → checklist "New app page" below
├── New API → Backend first, then Frontend API module + page
└── Admin-only → src/Admin, not Frontend
```

---

## Checklist: new authenticated page

### 1. Feature API module

```ts
// lib/features/billing/billing-api.ts
import { apiRequest } from "@/lib/api/http";

export const billingApi = {
  async getInvoices() {
    return apiRequest<InvoiceDto[]>({ path: "/api/v1/billing/invoices", auth: true });
  },
};
```

- Mirror backend route prefix `/api/v1/...`
- Use `idempotent: true` when backend requires `Idempotency-Key`
- Pass `rowVersion` in body/query when documented in [`OPTIMISTIC_CONCURRENCY.md`](../backend/OPTIMISTIC_CONCURRENCY.md)

### 2. Page + route

```
app/app/billing/page.tsx    → URL /app/billing
```

```tsx
"use client";

import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { billingApi } from "@/lib/features/billing/billing-api";

export default function BillingPage() {
  const { t } = useTranslation();
  const q = useQuery({
    queryKey: ["billing", "invoices"],
    queryFn: async () => {
      const res = await billingApi.getInvoices();
      if (!res.success || !res.data) throw new Error(res.error?.message ?? "Failed");
      return res.data;
    },
  });
  // ...
}
```

`app/app/layout.tsx` already wraps children with `RequireAuth`.

### 3. Navigation

```tsx
// app/app/layout.tsx — navItems
{ href: "/app/billing", label: t("nav.billing") },
```

### 4. i18n (all three locales)

Add keys to `i18n/locales/en.json`, `vi.json`, `zh.json`:

- `nav.billing`
- `billing.title`, `billing.*` (labels, errors, success)

Never ship English-only strings in JSX.

### 5. Shared helpers

- Dates in tables → `formatDateTimeOrDash`
- File sizes → `formatBytes`
- Lists → plan for `calcTotalPages` + future `useDebouncedValue`

### 6. Docs

- Update [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md)
- If new cross-cutting helper → [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md)

### 7. Verify

```bash
cd src/Frontend
npm run typecheck
# manual test on /app/billing
```

---

## Checklist: new public page

Example: `/pricing`

1. `app/pricing/page.tsx` (no `RequireAuth`)
2. Add links from landing if needed
3. i18n keys under `pricing.*`
4. `metadata` export if SEO matters:

```tsx
import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Pricing",
};
```

---

## Checklist: extend auth

| Change | Files |
|--------|-------|
| New auth endpoint | `lib/features/auth/auth-api.ts` |
| New public route | `app/<name>/page.tsx` |
| Login flow change | `app/login/page.tsx`, `lib/auth/auth-context.tsx` |

Use `apiRaw` when success status is not 200 (e.g. 2FA → 202).

---

## Checklist: new reusable UI

| Reuse count | Location |
|-------------|----------|
| 1 page only | Keep in `app/.../page.tsx` or colocate `components/<feature>/` |
| 2+ pages | `components/<FeatureCard>.tsx` |
| Design system atom | `components/ui/` |

---

## Query key convention (recommended)

Until `lib/constants/query-keys.ts` exists, use consistent arrays:

```ts
["me", "profile"]
["media", assetId]
["billing", "invoices", filters]
```

When the app grows, centralize in `lib/constants/query-keys.ts` (copy Admin pattern).

---

## New project from this base

1. Copy `src/Frontend` or use repo as template.
2. Replace landing (`app/page.tsx`, branding in `i18n`).
3. Remove demo routes you do not need (e.g. Media Lab).
4. Add product routes under `app/app/`.
5. Point `API_PROXY_TARGET` / production API URL to your deployment.
6. Configure `NEXT_PUBLIC_GOOGLE_CLIENT_ID` if using Google auth.

Keep `lib/api`, `lib/auth`, `lib/shared`, `i18n` structure intact.

---

## Anti-patterns

| Avoid | Do instead |
|-------|------------|
| `fetch("https://localhost:7124/...")` in browser dev | `/api/v1/...` via proxy |
| Hard-coded UI strings | `t("key")` + 3 locale files |
| Business logic in `components/ui` | Move to `lib/features` or page |
| Duplicate date formatting | `@/lib/shared` |
| Admin API in Frontend | Use Admin app |
| Large rewrite in same PR as feature | Minimal diff |

---

## AI assistants

Follow [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) and Cursor rule [`.cursor/rules/frontend.mdc`](../../.cursor/rules/frontend.mdc).
