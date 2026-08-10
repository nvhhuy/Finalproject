# Frontend feature catalog

Catalog of **existing** Frontend base features — routes, behavior, and backend APIs.

---

## Public routes

| Path | Page | Auth |
|------|------|------|
| `/` | Landing + feature list | Public |
| `/login` | Sign-in (+ 2FA step), Google | Public |
| `/register` | Create account | Public |
| `/forgot-password` | Request reset email | Public |
| `/reset-password` | `?tokenId=&token=` from email link | Public |
| `/verify-email` | `?tokenId=&token=` from email link | Public |
| `/pricing` | Plans from API, CTA → subscribe | Public |
| `/about` | About page | Public |
| `/contact` | Contact page | Public |
| `/t/[slug]` | Tenant public landing + org SSO sign-in | Public |
| `/t/[slug]/app` | Set `X-Tenant-Id` context → main app | Auth |

---

## Authenticated routes (`/app`)

| Path | Page | Nav |
|------|------|-----|
| `/app` | Dashboard (wallet, org, tickets summary) | ✓ |
| `/app/books` | Personal book catalog — create, edit, delete, search (own books only) | ✓ |
| `/app/profile` | View/edit profile, avatar demo, resend verification | ✓ |
| `/app/security/2fa` | Begin/confirm 2FA + QR | ✓ |
| `/app/media` | Media lab (upload flow) | ✓ |
| `/app/support` | Support tickets (create, reply, close, reopen, attachments) | ✓ |
| `/app/notifications` | Full notification list | ✓ |
| `/app/settings` | PWA install + Web Push enable/disable | ✓ |
| `/app/wallet` | Personal credits (packages, demo top-up, metered debit) | ✓ |
| `/app/subscribe` | MVP tenant + subscription checkout | ✓ |
| `/app/tenant` | Org usage, billing actions (plan switch/cancel), domains, SSO, invite members | ✓ |

Guard: `RequireAuth` in `app/app/layout.tsx`.

**Tenant header:** authenticated API calls send `X-Tenant-Id` from `localStorage` (`lib/tenancy/tenant-context.ts`) when set.

---

## Authentication

| Capability | UI | API |
|------------|-----|-----|
| Email + password login | `/login` | `POST /api/v1/auth/login` |
| 2FA challenge | Second step | HTTP **202** + `POST /auth/login/2fa` |
| Register | `/register` | `POST /auth/register` (idempotent) → redirects to `/awaiting-verification` (no app access) |
| Awaiting verification | `/awaiting-verification` | Resend link; blocks `/app` until verified |
| Refresh session | Auto in `http.ts` | `POST /auth/refresh` |
| Forgot password | `/forgot-password` | `POST /auth/forgot-password` |
| Reset password | `/reset-password` | `POST /auth/reset-password` |
| Verify email | `/verify-email` | `POST /auth/verify-email` |
| Resend verification | Register + Profile | `POST /auth/resend-verification` |
| Google Sign-In | `/login` | `POST /auth/google` |
| Tenant OIDC SSO | `/t/[slug]` | `GET /public/tenants/{slug}/sso` + `POST /auth/tenant-sso/{slug}` |
| Logout | App header | Clear `localStorage` (client-only) |

---

## Profile

| Capability | Notes | API |
|------------|-------|-----|
| Load profile | Hydrate on app load + profile page | `GET /users/profile` |
| Update name | `rowVersion` optimistic concurrency | `PUT /users/profile` |
| Resend verification | Button on profile | `POST /auth/resend-verification` |
| Change password | Link → forgot-password flow | `POST /auth/forgot-password` |
| Avatar demo | Upload via Media flow | Media APIs |

---

## Books

| Capability | API |
|------------|-----|
| List (search, paged) / get / create / update / delete | `GET/POST/PUT/DELETE /api/v1/books` (`lib/features/books/books-api.ts`) |

Private per-user catalog; update/delete pass `rowVersion` for optimistic concurrency (mirrors the Media delete pattern).

---

## Support

| Capability | API |
|------------|-----|
| Create / list / detail | `POST/GET /support/tickets` |
| Reply + attachments | `POST .../messages` (media upload → storageKey) |
| Close / reopen | `POST .../close`, `POST .../reopen` |

---

## Tenancy & billing

| Capability | UI | API |
|------------|-----|-----|
| Invite member | `/app/tenant` | `POST /tenants/{id}/members` |
| Custom domains + SSO config | `/app/tenant` | `tenancy-api` |
| Entitlements, invoices, cancel, change plan | `/app/tenant` | `billing-api` |
| Subscribe new tenant | `/app/subscribe` | `POST /billing/subscribe` |

---

## Notifications

| Capability | UI | API |
|------------|-----|-----|
| In-app list + mark read | Bell widget + `/app/notifications` | `GET /notifications`, `POST /notifications/{id}/read` |
| Realtime push | SignalR (dev: `/hubs` proxy + long polling) | `/hubs/notifications` |
| Web Push | Settings page + service worker `/sw.js` | `POST/DELETE .../push/subscribe`, VAPID key API |
| PWA install | Settings + `manifest.webmanifest` | Browser install prompt / iOS steps |

---

## Intentionally out of scope (use other apps)

| Feature | Notes |
|---------|--------|
| Admin users / audit / DLQ / platform plans CRUD | `src/Admin` |
| Profile `avatarUrl` field on API | Needs backend DTO + migration |
| User self-service disable 2FA | Admin reset only |
| Media asset gallery | No backend list endpoint |

---

## When you add a feature

Update this file with route, API, and i18n key groups.
