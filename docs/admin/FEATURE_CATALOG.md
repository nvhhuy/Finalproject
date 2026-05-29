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
| `/notifications` | Broadcast in-app announcement to all members | Admin |
| `/plans` | Subscription plan catalog (create, edit, delete) | Admin |
| `/credit-packages` | Wallet top-up SKUs (price → credits) | Admin |
| `/tenants` | Tenant list, create, assign/change subscription plan | Admin |
| `/invoices` | Platform billing invoices (paginated) | Admin |

Nav sidebar: Dashboard, Users, Announcements, Plans, Tenants, Invoices, Audit logs, Settings, DLQ, Support tickets.

## Subscription plans

| Capability | Notes |
|------------|-------|
| List plans | Cards with price, currency, limits |
| Create plan | Modal; currency dropdown (ISO 4217 subset) |
| Edit plan | Update price, currency, trial, limits, active flag; code is read-only |
| Delete plan | Only when no subscriptions reference the plan; otherwise deactivate |

API: `GET/POST/PUT/DELETE /api/v1/platform/plans`.

## Credit packages

| Feature | Notes |
|---------|-------|
| List packages | Price, currency, credits amount, active flag |
| Create / edit / delete | Same pattern as plans; codes immutable after create |

API: `GET/POST/PUT/DELETE /api/v1/platform/credit-packages`. Public catalog: `GET /api/v1/public/credit-packages`.

## Tenants

| Capability | Notes |
|------------|-------|
| List tenants | Paginated table; shows slug, status, owner link, subscription plan/status |
| Create tenant | Modal: slug, display name, owner user ID (must exist), optional branding |
| Assign / change plan | Modal; active plans only; calls platform subscription API |

API: `GET/POST /api/v1/platform/tenants`, `GET/POST /api/v1/platform/tenants/{id}/subscription`.

**Workflow:** Create user (owner) → create tenant → assign plan so entitlements apply.

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
| System settings backup run/history/restore/download | Buttons + list | `POST /run`, `GET /history`, `GET /download?key=`, `POST /restore` under `/api/v1/admin/settings/system-backup` |
| System settings import-restore (legacy migration) | File upload + confirm `IMPORT_RESTORE_SETTINGS` | `POST /import-restore` (multipart) |
| Database backup (toggle + retention) | Checkbox + number input | `GET/PUT /api/v1/admin/settings/database-backup` |
| Database backup run/history/restore/download | Buttons + list | `POST /run`, `GET /history`, `GET /download?key=`, `POST /restore` under `/api/v1/admin/settings/database-backup` |
| Database import-restore (legacy migration) | File upload + confirm `IMPORT_RESTORE_DATABASE` | `POST /import-restore` (multipart; `pg_dump -Fc`) |

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

## Member announcements (`/notifications`)

| Capability | Notes |
|------------|-------|
| Compose title, body, optional link | Link must be a relative path (e.g. `/app/settings`) |
| Send to all active members | Role `User`, not locked/deleted/closed |
| Confirmation | Browser confirm before send |
| Audit | `notifications.member_broadcast` with recipient count |

API: `POST /api/v1/admin/notifications/broadcast` (idempotent). Live SignalR delivery is retried by the worker for pending rows.

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

## Billing invoices

| Capability | Notes |
|------------|-------|
| List invoices | Paginated; tenant ID, status, amount, provider |

API: `GET /api/v1/platform/invoices`.

---

## Explicitly not in Admin

- Public user self-service (profile, media upload UI for end users)
- Admin self-registration
- Multi-tenant org switcher
- Tenant suspend/activate or delete (API not exposed yet)
- Real-time websockets / live audit stream
- Role management UI beyond assigning User/Admin on user form

The items above can be added following [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) if the backend already supports them.
