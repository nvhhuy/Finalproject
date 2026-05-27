# Frontend common libraries

Shared helpers so pages stay thin. **Import from `@/lib/shared`** instead of reimplementing.

Mirror of Admin [`docs/admin/COMMON_LIBRARIES.md`](../admin/COMMON_LIBRARIES.md) — same function names where possible.

---

## Import cheat sheet

```ts
import {
  formatDate,
  formatDateTime,
  formatRelativeTime,
  formatCount,
  formatBytes,
  formatDateOrDash,
  formatDateTimeOrDash,
  formatEmpty,
  dash,
  calcTotalPages,
  clampPage,
  createAuthSchemas,
  firstZodError,
  ApiClientError,
  isConcurrencyError,
} from "@/lib/shared";
```

---

## Format (`lib/shared/format/`)

| Function | Use when |
|----------|----------|
| `formatDate(value)` | Date only, active i18n locale |
| `formatDateTime(value)` | Date + time |
| `formatRelativeTime(value)` | "2 hours ago" style |
| `formatCount(n)` | Localized integer |
| `formatBytes(n)` | Human-readable file size |

Locale: `en` → `en-US`, `vi` → `vi-VN`, `zh` → `zh-CN` (via `i18n.language`).

**Do not** use `new Date(x).toLocaleDateString()` in pages.

---

## Display (`lib/shared/display.ts`)

| Function | Use when |
|----------|----------|
| `dash()` | Localized em dash for empty cells |
| `formatEmpty(value)` | Nullable string → dash if empty |
| `formatDateOrDash(utc?)` | Table cell datetime optional |
| `formatDateTimeOrDash(utc?)` | Same with time |

---

## Pagination (`lib/shared/pagination.ts`)

| Function | Use when |
|----------|----------|
| `calcTotalPages(total, pageSize)` | 1-based page count |
| `clampPage(page, totalPages)` | Bound page index |

Add `usePagination` hook under `lib/hooks/` when a second list page needs it.

---

## Validation (`lib/shared/validation.ts`)

| Function | Use when |
|----------|----------|
| `createAuthSchemas(t)` | Zod schemas with i18n messages |
| `firstZodError(error)` | Show first validation message |

Pass `t` from `useTranslation()` so messages follow locale.

---

## Errors (`lib/errors.ts`)

| Export | Use when |
|--------|----------|
| `ApiClientError` | Typed API failure |
| `isConcurrencyError(err)` | HTTP 409 / row version conflict → show refresh message |

Example:

```ts
import { isConcurrencyError } from "@/lib/shared";

onError: (err) => {
  if (isConcurrencyError(err)) {
    setError(t("errors.concurrencyConflict"));
    return;
  }
  // ...
};
```

---

## API client (`lib/api/`)

| Module | Use when |
|--------|----------|
| `apiRequest<T>({ path, method, body, auth, idempotent })` | Standard envelope calls |
| `apiRaw<T>(path, init)` | Login 202, non-standard status |
| `newIdempotencyKey()` | Register, media complete |
| `getApiBaseUrl()` | Rare; prefer relative `/api/v1/...` in browser |

---

## UI utilities

| Module | Use when |
|--------|----------|
| `cn()` from `@/lib/ui/cn` | Merge Tailwind classes |
| `Button`, `Input` from `@/components/ui` | Consistent primitives |

---

## Adding a new shared helper

1. Implement in `lib/shared/<area>.ts` (pure function preferred).
2. Export from `lib/shared/index.ts`.
3. Document in this file.
4. Add unit tests only if logic is non-trivial (optional for format wrappers).

**Do not** put React hooks in `lib/shared/` — use `lib/hooks/` instead.
