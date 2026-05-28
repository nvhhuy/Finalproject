# Admin feature catalog

Catalog of **existing** Admin Console features — routes, behavior, and corresponding backend APIs.

---

## Routes

| Path | Page | Auth |
|------|------|------|
| `/login` | Sign-in (+ 2FA step) | Public |
| `/` | Dashboard | Admin |
| `/users` | User list + create/edit dialog | Admin |
| `/users/:userId` | User detail (actions) | Admin |
| `/audit-logs` | Audit log table | Admin |
| `/settings` | Runtime toggles | Admin |
| `/messaging` | DLQ peek / replay / drain | Admin |
| `/support` | Support tickets operations | Admin |

Nav sidebar: Dashboard, Users, Audit logs, Settings, DLQ, Support tickets.

---

## Authentication

| Capability | UI | API |
|------------|-----|-----|
| Email + password login | `LoginPage` | `POST /api/v1/auth/login` |
| 2FA completion | Second step on login | `POST /api/v1/auth/2fa` (flow via auth-api) |
| Session refresh | Automatic in `client.ts` | `POST /api/v1/auth/refresh` |
| Logout | Sidebar | Clears Zustand + sessionStorage |
| Role gate | `ProtectedRoute` | JWT must include **Admin** role |

**Token storage:** access token in memory (Zustand); refresh token in `sessionStorage`.

---

## Dashboard

| Capability | Notes |
|------------|-------|
| Active user count | Card metric |
| Registrations last 14 days | Bar chart by day |
| Error state | Alert when API unreachable |

API: `GET /api/v1/admin/metrics/dashboard` → `getDashboardMetrics()`.

---

## Users

### List (`/users`)

| Capability | Notes |
|------------|-------|
| Search | Debounced email/username |
| Filters | Locked / active, role |
| Sort | Username, email, registered, role; asc/desc |
| Pagination | 20 per page |
| Create user | Modal form |
| Edit user | Modal (loads detail + `rowVersion`) |
| Manage | Link to detail |

API: `GET /api/v1/admin/users` with query params.

### Detail (`/users/:userId`)

| Action | Requires `rowVersion` |
|--------|----------------------|
| Lock / unlock | Yes |
| Revoke all sessions | Yes |
| Reset password | Yes (modal) |
| Reset 2FA | Yes |
| Mark email verified | Yes |
| Resend verification email | Yes |
| Impersonate (non-Admin targets) | Yes — shows tokens once |
| Soft delete | Yes + confirm |
| View audit logs | Link with `?targetUserId=` |

API prefix: `/api/v1/admin/users/{id}/...`

Concurrency: see [`docs/backend/OPTIMISTIC_CONCURRENCY.md`](../backend/OPTIMISTIC_CONCURRENCY.md).

---

## Settings

| Setting | UI control | API |
|---------|------------|-----|
| Allow public registration | Checkbox | `GET/PUT` allow-public-register admin settings |
| System settings backup (toggle + retention) | Checkbox + number input | `GET/PUT /api/v1/admin/settings/system-backup` |
| System settings backup run/history/restore | Buttons + list | `POST /run`, `GET /history`, `POST /restore` under `/api/v1/admin/settings/system-backup` |
| Database backup (toggle + retention) | Checkbox + number input | `GET/PUT /api/v1/admin/settings/database-backup` |
| Database backup run/history/restore | Buttons + list | `POST /run`, `GET /history`, `POST /restore` under `/api/v1/admin/settings/database-backup` |

Admin **does not** expose end-user registration UI — only toggles whether the **public Frontend** may call `POST /auth/register`.

---

## Audit logs

| Capability | Notes |
|------------|-------|
| Paginated table | 50 per page |
| Filter by target user ID | Query param + URL `?targetUserId=` |
| Columns | Time, action, actor, target, details |

API: `GET /api/v1/admin/audit-logs`.

---

## Dead-letter queue (DLQ)

| Capability | Notes |
|------------|-------|
| Peek messages | Manual trigger (query `enabled: false` until Peek) |
| Replay | Confirm → republish to primary queue |
| Drain | Confirm → discard (irreversible) |
| Queue name + batch size | Configurable inputs |

Requires RabbitMQ enabled on API. API: `/api/v1/admin/messaging/dlq/...`.

---

## Internationalization (i18n)

| Locale | Code | File |
|--------|------|------|
| English | `en` | `i18n/locales/en.json` |
| Vietnamese | `vi` | `i18n/locales/vi.json` |
| Chinese (Simplified) | `zh` | `i18n/locales/zh.json` |

- Persisted: `localStorage` key `admin-locale`
- Switcher: header (compact) + sidebar + login page
- API error messages from server may remain English; UI strings use `t('...')`

---

## UI / UX cross-cutting

| Feature | Implementation |
|---------|----------------|
| Toast notifications | `shared/lib/toast` + `Toaster` |
| Responsive layout | Mobile drawer sidebar; `PageContent` padding |
| Loading states | Query `isLoading` / mutation `isPending` |
| Modals | `shared/components/ui/Modal` |
| Forms | `Input`, `Button`, Zod + `shared/lib/validation` |

---

## Explicitly not in Admin

- Public user self-service (profile, media upload UI for end users)
- Admin self-registration
- Multi-tenant org switcher
- Real-time websockets / live audit stream
- Role management UI beyond assigning User/Admin on user form

The items above can be added following [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) if the backend already supports them.
