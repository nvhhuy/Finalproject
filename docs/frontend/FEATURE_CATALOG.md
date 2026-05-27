# Frontend feature catalog

Mục lục tính năng **đã có** trong base Frontend — routes, hành vi, API backend.

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

---

## Authenticated routes (`/app`)

| Path | Page | Nav |
|------|------|-----|
| `/app` | Dashboard (user summary) | ✓ |
| `/app/profile` | View/edit profile, avatar demo, resend verification | ✓ |
| `/app/security/2fa` | Begin/confirm 2FA + QR | ✓ |
| `/app/media` | Media lab (upload flow) | ✓ |

Guard: `RequireAuth` in `app/app/layout.tsx`.

---

## Authentication

| Capability | UI | API |
|------------|-----|-----|
| Email + password login | `/login` | `POST /api/v1/auth/login` |
| 2FA challenge | Second step | HTTP **202** + `POST /auth/login/2fa` |
| Register | `/register` | `POST /auth/register` (idempotent) |
| Refresh session | Auto in `http.ts` | `POST /auth/refresh` |
| Forgot password | `/forgot-password` | `POST /auth/forgot-password` |
| Reset password | `/reset-password` | `POST /auth/reset-password` |
| Verify email | `/verify-email` | `POST /auth/verify-email` |
| Resend verification | Register + Profile | `POST /auth/resend-verification` |
| Google Sign-In | `/login` | `POST /auth/google` (needs `NEXT_PUBLIC_GOOGLE_CLIENT_ID`) |
| Logout | App header | Clear `localStorage` (client-only) |

Client: `lib/features/auth/auth-api.ts`.

---

## Profile

| Capability | Notes | API |
|------------|-------|-----|
| Load profile | Hydrate on app load + profile page | `GET /users/profile` |
| Update name | `rowVersion` optimistic concurrency | `PUT /users/profile` |
| Resend verification | Button on profile | `POST /auth/resend-verification` |
| Avatar demo | Upload via Media flow (no profile field yet) | Media APIs |

Client: `lib/features/users/user-api.ts`.

---

## Two-factor authentication

| Capability | API |
|------------|-----|
| Begin setup (shared key + otpauth URI) | `POST /auth/2fa/begin` |
| Confirm TOTP code | `POST /auth/2fa/confirm` |
| Status display | From profile `twoFactorEnabled` |

UI: QR via `components/auth/QrCode.tsx`.

**Not in base:** user self-service **disable** 2FA (Admin can reset via admin API).

---

## Media lab

| Step | API |
|------|-----|
| Initiate upload (presigned URL) | `POST /media/uploads` |
| PUT bytes to storage | Presigned URL (direct) |
| Complete + enqueue processing | `POST /media/{id}/complete` (idempotent) |
| Poll asset status | `GET /media/{id}` |
| Delete | `DELETE /media/{id}?rowVersion=` |

Client: `lib/features/media/media-api.ts`.

**Not in base:** list all user media assets (no backend list endpoint).

---

## i18n

| Locale | File |
|--------|------|
| English | `i18n/locales/en.json` |
| Tiếng Việt | `i18n/locales/vi.json` |
| 中文 | `i18n/locales/zh.json` |

Switcher: `components/layout/LanguageSwitcher.tsx`.

---

## Intentionally out of scope (use other apps / future work)

| Feature | Notes |
|---------|--------|
| Admin users / audit / DLQ | `src/Admin` |
| SignalR `/hubs/notifications` | Backend `Realtime:Enabled` — no client yet |
| Change password while logged in | Use forgot/reset flow; no dedicated user endpoint |
| Profile `avatarUrl` field | Needs backend DTO + migration |
| Product-specific checkout, CMS, etc. | Add under `app/` + `lib/features/` per project |

---

## When you add a feature

Update this file with route, API, and i18n key groups — keeps PM and AI aligned.
