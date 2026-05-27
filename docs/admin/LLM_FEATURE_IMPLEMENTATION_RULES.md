# LLM / AI feature implementation rules (Admin)

Strict guide for Cursor and other AI assistants when changing **`src/Admin/`** only.

Human developers should follow the same rules where practical.

---

## Scope

- **In scope:** `src/Admin/**`
- **Out of scope:** `src/Backend/**`, `src/Frontend/**` (use their own LLM docs)
- **Docs:** update `docs/admin/` when adding user-visible features or new patterns

---

## Non-negotiable rules

1. **Do not break the feature / shared boundary**
   - `features/*` → may import `shared/*`, `i18n/*`, `config/*`, `app/*` types only indirectly via hooks
   - `shared/*` → **must not** import from `features/*`

2. **All user-visible strings must use i18n**
   - `const { t } = useTranslation()` + `t('key')`
   - Add keys to **en, vi, and zh** JSON files together
   - Exception: server error `message` text returned as-is from API

3. **Use shared format helpers for dates and empty cells**
   - `formatDate`, `formatDateTime`, `formatDateOrDash`, `formatDateTimeOrDash`, `formatEmpty`
   - Never add raw `toLocaleDateString()` / `toLocaleString()` in feature pages

4. **Use centralized query keys**
   - Define in `shared/constants/query-keys.ts`
   - Invalidate via `queryKeys.<domain>.all()` or specific list keys

5. **API calls only through `shared/api`**
   - Extend `admin-api.ts` / `auth-api.ts`; use `apiRequest` from `client.ts`
   - Mutations: `idempotent: true` where backend expects idempotency
   - Include `rowVersion` when backend requires optimistic concurrency

6. **Minimize diff scope**
   - No unrelated refactors, formatting sweeps, or dependency upgrades unless asked
   - Match existing component patterns (`PageHeader`, `PageContent`, `Card`, `Button`)

7. **TypeScript strict**
   - No `any` without justification
   - Run `npx tsc -b` in `src/Admin` after substantive changes

---

## File placement rules

| What | Where |
|------|-------|
| New screen | `features/<domain>/<Name>Page.tsx` |
| Feature-only dialog | `features/<domain>/<Name>Dialog.tsx` |
| Reusable UI atom | `shared/components/ui/` |
| Reusable layout | `shared/components/layout/` |
| Reusable table/pagination | `shared/components/patterns/` |
| Pure functions | `shared/lib/` (+ export from `shared/lib/index.ts`) |
| React hooks (2+ consumers) | `shared/hooks/` |
| DTO types | `shared/types/api.ts` |
| Routes | `app/router.tsx` |
| Nav item | `shared/components/layout/AdminLayout.tsx` `navItems` |

---

## Implementation patterns

### Pages

```tsx
// Required imports pattern
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { PageHeader } from '@/shared/components/layout/PageHeader'
import { PageContent } from '@/shared/components/layout/PageContent'
import { queryKeys } from '@/shared/constants/query-keys'
```

- Loading: `t('common.loading')` or skeleton text
- Errors: `Alert` or `toastApiError`
- Success mutations: `toast.success(t('toast....'))`

### Forms

- Validate with Zod via `createUserFormSchemas`, `createLoginSchemas`, or `createValidators(t)`
- Show first error with `firstZodError(parsed, t)`
- Map API errors with `getApiErrorMessage(err, 'errors....')`

### Lists with pagination

- `usePagination()` + `useTotalPages(totalCount, pageSize)`
- Footer: `<DataTablePagination ... />`
- Search: `useDebouncedValue` + reset page on filter change

### Destructive actions

- `confirmAction(t('...confirmKey'))` before mutation
- Prefer `runAdminAction` for user detail-style operations

---

## i18n rules

- Key naming: `feature.camelCase` or `feature.section.label`
- Reuse `common.*` for Cancel, Save, Loading, Previous, Next
- Pluralization: use `_other` suffix keys where i18next plural is needed (see `users.usersFound`)
- After adding keys, verify all three locale files have the same key paths

---

## Styling rules

- Tailwind utility classes; use `cn()` for conditional classes
- Select elements: `formSelectClass` from `shared/constants/styles`
- CSS variables: `var(--color-surface)`, `var(--color-border)`, `var(--color-accent)`
- Responsive: mobile-first; test `lg:` sidebar behavior

Do not introduce CSS modules or styled-components unless explicitly requested.

---

## Security rules

- Do not log or commit tokens, `.env` secrets, or impersonation payloads
- Do not move refresh token to `localStorage`
- Do not bypass `ProtectedRoute` for admin screens
- Impersonation UI must keep warning copy (i18n)

---

## PR / change quality checklist (AI must self-check)

- [ ] `npx tsc -b` passes in `src/Admin`
- [ ] New routes added to `router.tsx` and `navItems` if user-facing
- [ ] `queryKeys` updated
- [ ] i18n: en + vi + zh
- [ ] `docs/admin/FEATURE_CATALOG.md` updated for new capabilities
- [ ] No new English-only UI strings
- [ ] Dates/empty cells use shared format helpers

---

## Forbidden shortcuts

- Giant copy-paste of pagination markup
- `window.alert` instead of toast/Alert patterns
- Direct `fetch()` bypassing `apiRequest`
- Hard-coded API URLs (use `API_V1` from `config/env.ts`)
- Installing new UI frameworks without user approval
- Removing i18n or auth checks to "fix" tests quickly

---

## When backend is missing

Stop and tell the user:

1. Which endpoint/DTO is needed
2. Suggest implementing backend first per `docs/backend/BASE_GUIDE.md`

Do not mock permanent fake APIs in production code paths unless asked for a UI-only prototype.

---

## Related Cursor rule

Project rule file: [`.cursor/rules/admin.mdc`](../../.cursor/rules/admin.mdc) — applies when editing files under `src/Admin/`.
