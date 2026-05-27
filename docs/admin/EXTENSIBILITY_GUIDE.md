# Admin extensibility guide

Step-by-step workflow for adding or extending Admin features without breaking conventions.

---

## Before you start

1. Confirm the **backend admin endpoint** exists (or implement it first — see `docs/backend/BASE_GUIDE.md`).
2. Read [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) to avoid duplicating screens.
3. Skim [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) for helpers to reuse.

---

## Checklist: new page (full feature)

### 1. Types & API

| Step | File |
|------|------|
| Add DTO types | `shared/types/api.ts` |
| Add fetch/mutation functions | `shared/api/admin-api.ts` |
| Use `idempotent: true` on POST/PUT/DELETE | Same |
| Pass `rowVersion` on user-like updates | Same |

### 2. Query keys

```ts
// shared/constants/query-keys.ts
export const queryKeys = {
  // ...
  reports: {
    all: () => ['admin', 'reports'] as const,
    list: (params: unknown) => ['admin', 'reports', params] as const,
  },
}
```

### 3. Feature folder

```
features/<feature>/
  MyFeaturePage.tsx
  MyFeatureFormDialog.tsx   # optional
```

### 4. Route

```ts
// app/router.tsx
import { MyFeaturePage } from '@/features/my-feature/MyFeaturePage'

// inside ProtectedRoute children:
{ path: 'my-feature', element: <MyFeaturePage /> },
```

### 5. Navigation

```ts
// shared/components/layout/AdminLayout.tsx — navItems
{ to: '/my-feature', labelKey: 'nav.myFeature', icon: SomeIcon },
```

### 6. i18n (all three locales)

Add keys under logical groups in:

- `i18n/locales/en.json`
- `i18n/locales/vi.json`
- `i18n/locales/zh.json`

Minimum: `nav.*`, page `title`/`description`, table headers, toasts, errors.

### 7. Page implementation template

```tsx
export function MyFeaturePage() {
  const { t } = useTranslation()
  const { data, isLoading, error } = useQuery({
    queryKey: queryKeys.reports.list(params),
    queryFn: () => listReports(params),
  })

  return (
    <>
      <PageHeader title={t('myFeature.title')} description={t('myFeature.description')} />
      <PageContent>
        {error ? <Alert variant="error">{...}</Alert> : null}
        {/* content */}
      </PageContent>
    </>
  )
}
```

### 8. Documentation

Update [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) with the new route and capabilities.

---

## Checklist: extend existing page

| Change | Touch |
|--------|-------|
| New filter field | Page state + `queryKeys` params + API query string |
| New table column | JSX + i18n header key |
| New row action | `admin-api` function + button + toast / `runAdminAction` |
| New modal form | `Modal` + Zod schema in `validation.ts` or feature-local `useMemo` |

Invalidate queries on success:

```ts
void queryClient.invalidateQueries({ queryKey: queryKeys.users.all() })
```

---

## i18n expansion

1. Add English keys first (source of truth for structure).
2. Copy structure to `vi.json` and `zh.json` with translations.
3. Use `t('key')` in JSX; never hard-code user-visible strings in new code.
4. Zod: extend `createValidators` or add `createXxxSchemas(t)` in `validation.ts`.

---

## Concurrency (user mutations)

Any update/delete that backend protects with row version:

1. Load detail including `rowVersion`.
2. Send `rowVersion` on mutation body or query.
3. On `ConcurrencyConflict`, user sees translated message — ask to refresh.

See [`docs/backend/OPTIMISTIC_CONCURRENCY.md`](../backend/OPTIMISTIC_CONCURRENCY.md).

---

## Optional: extract to `patterns/`

When **two or more** features need the same UI block:

- Filter bar layout
- Status badge mapping
- Empty state illustration

Move to `shared/components/patterns/` with props, not feature imports.

---

## Testing manually

| Area | Check |
|------|-------|
| Happy path | Data loads, mutation succeeds, toast shows |
| API down | Alert / error toast |
| Mobile width | Sidebar drawer, header language switcher |
| Locale switch | Labels update; dates re-format on navigation |
| Auth | Redirect to login when session cleared |

Automated E2E is not set up in base — manual QA per feature is expected.

---

## Anti-patterns (avoid)

- Fetching in `useEffect` instead of TanStack Query
- Inline `['admin', 'users']` strings instead of `queryKeys`
- English-only strings in new UI
- Duplicating pagination JSX
- Importing feature code from `shared/`
- Storing access token in `localStorage` (use existing auth store pattern)

---

## Future extension ideas

| Idea | Suggested approach |
|------|-------------------|
| Reports / analytics | New `features/reports/`, chart library if needed |
| Bulk user actions | Table selection state + batch API + confirm modal |
| Live audit tail | WebSocket hook in `shared/hooks`, separate from Query polling |
| Dark/light theme | CSS variables already — add toggle in settings + persist |
| More locales | Add `i18n/locales/<code>.json` + `SUPPORTED_LOCALES` in `locales.ts` |
| Shared package with Frontend | Extract `api` types to monorepo package — only if both apps stabilize |

Keep Admin focused on **operations**; end-user product UI stays in `src/Frontend`.
