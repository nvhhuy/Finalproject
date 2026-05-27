# Admin common libraries

Shared helpers so feature code stays thin. **Prefer importing from `@/shared/lib`** (barrel) instead of reimplementing.

---

## Import cheat sheet

```ts
import {
  formatDate,
  formatDateTime,
  formatDateOrDash,
  formatDateTimeOrDash,
  formatEmpty,
  dash,
  toast,
  toastApiError,
  getApiErrorMessage,
  runAdminAction,
  confirmAction,
  copyWithToast,
  createLoginSchemas,
  createUserFormSchemas,
  firstZodError,
  calcTotalPages,
} from '@/shared/lib'

import { queryKeys } from '@/shared/constants/query-keys'
import { formSelectClass } from '@/shared/constants/styles'
import { usePagination, useTotalPages } from '@/shared/hooks/usePagination'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
```

---

## Format (`shared/lib/format/`)

| Function | Use when |
|----------|----------|
| `formatDate(value)` | Date only, respects active locale |
| `formatDateTime(value)` | Date + time |
| `formatCount(n)` | Localized number (metrics) |
| `formatDateOrDash(utc?)` | Table cell; null → em dash |
| `formatDateTimeOrDash(utc?)` | Same for datetime |

Locale follows `i18n.language` (`en` → `en-US`, `vi` → `vi-VN`, `zh` → `zh-CN`).

**Do not** call `new Date(x).toLocaleDateString()` in feature pages.

---

## Display (`shared/lib/display.ts`)

| Function | Use when |
|----------|----------|
| `dash()` | Localized `—` |
| `formatEmpty(value)` | Nullable string with dash fallback |

---

## Toast & API errors (`shared/lib/toast.ts`)

```ts
toast.success(t('toast.userCreated'))
toast.error('...')
toastApiError(err, 'toast.settingsUpdateFailed')
getApiErrorMessage(err, 'errors.requestFailed')
```

`ConcurrencyConflict` is mapped to a friendly i18n message automatically.

---

## Admin actions (`shared/lib/admin-action.ts`)

Wraps try/catch + success/error toasts for user detail-style operations:

```ts
await runAdminAction(t, 'userDetail.actions.unlock', () => unlockUser(id, rowVersion()), {
  onSuccess: invalidate,
})
```

---

## Confirm & clipboard

```ts
if (confirmAction(t('userDetail.deleteConfirm'))) { ... }
await copyWithToast(token, t('userDetail.accessToken'))
```

---

## Validation (`shared/lib/validation.ts`)

Zod schemas with i18n messages:

```ts
const { t } = useTranslation()
const schemas = useMemo(() => createLoginSchemas(t), [t])
const err = firstZodError(schemas.login.safeParse(data), t)
```

User forms: `createUserFormSchemas(t)` → `.base` / `.create`.

---

## Query keys (`shared/constants/query-keys.ts`)

Centralize TanStack Query keys:

```ts
queryKeys.users.list(params)
queryKeys.users.detail(userId)
queryKeys.users.all()           // prefix invalidate
queryKeys.auditLogs.list(page, targetUserId)
queryKeys.metrics.dashboard()
```

**Always** add new keys here when introducing new queries.

---

## Pagination

```ts
const { page, setPage, resetPage } = usePagination()
const totalPages = useTotalPages(data?.totalCount, PAGE_SIZE)
```

UI: `<DataTablePagination page={page} totalPages={totalPages} onPageChange={setPage} />`

---

## Styles (`shared/constants/styles.ts`)

- `formSelectClass` — native `<select>` in filters/forms
- `formSelectCompactClass` — language switcher compact variant

---

## Hooks

| Hook | Purpose |
|------|---------|
| `useDebouncedValue(value, ms)` | Search inputs (default 300ms; Users uses 350) |
| `usePagination()` | `page`, `setPage`, `resetPage` |
| `useTotalPages(count, pageSize)` | Derived total pages |

---

## Components (`shared/components/patterns/`)

| Component | Purpose |
|-----------|---------|
| `DataTablePagination` | Previous / page indicator / Next footer |

Add new cross-feature UI here, not inside a single feature folder.

---

## i18n

- Keys in `i18n/locales/{en,vi,zh}.json` — **keep all three in sync**.
- Use nested keys: `users.createDialogTitle`, `toast.userCreated`.
- Interpolation: `t('common.pageOf', { page, total })`.

Switch locale: `setAppLocale('vi')` from `@/i18n/config`.

---

## When to add vs reuse

| Situation | Action |
|-----------|--------|
| Second page needs same date format | Use `formatDateOrDash` |
| Third table needs pagination footer | Use `DataTablePagination` |
| New resource list + detail | Add `queryKeys.<resource>` |
| One-off UI only used once | Keep in feature until second use |

Avoid a giant `utils.ts` — add a focused file under `shared/lib/` and export from `index.ts`.
