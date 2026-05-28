# Test catalog

Inventory of automated tests as of the current baseline. Update this file when adding **user-visible flows** or **new integration scenarios**.

---

## Summary

| Layer | Project / folder | Approx. specs | Requires |
|-------|------------------|---------------|----------|
| API integration | `Api.IntegrationTests` | 15 active + 1 optional skip | Postgres (Testcontainers or `TEST_DATABASE_CONNECTION_STRING`) |
| Playwright — Frontend | `e2e/tests/frontend/` | 9 (+ 1 setup) | Postgres + migrated DB; Playwright `webServer` |
| Playwright — Admin | `e2e/tests/admin/` | 6 | Same as Frontend |

---

## API integration tests

**Project:** `src/Backend/Tests/Api.IntegrationTests`  
**Run:** `dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj`

| File | Scenario |
|------|----------|
| `PlatformIntegrationTests` | `GET /health/live`, `GET /health/ready` |
| `AuthIntegrationTests` | Health smoke; register does not issue tokens until verified |
| `AuthFlowIntegrationTests` | Register → verify → login → refresh; forgot/reset password; resend verification |
| `UsersIntegrationTests` | Profile GET/PATCH with `rowVersion` |
| `AdminIntegrationTests` | Admin login; user CRUD lock/unlock/soft-delete; email verify, reset password, revoke sessions; impersonation; settings, audit logs, DLQ (Rabbit disabled) |
| `MediaIntegrationTests` | Media initiate/complete/get (**skipped** unless `INTEGRATION_MEDIA_ENABLED=true`) |

**Helpers:** `Support/IntegrationTestApp.cs`, `CapturingEmailSender`, `IntegrationHttpExtensions`, `EmailBodyParser`.

**Factory:** `CustomWebApplicationFactory` — test email sink, seed admin, ephemeral DataProtection directory per collection.

---

## Playwright — Frontend

**Folder:** `e2e/tests/frontend/`  
**Base URL:** `E2E_FRONTEND_URL` (default `http://localhost:3000`)

| File | Describe block | Coverage |
|------|----------------|----------|
| `auth.setup.ts` | setup `authenticate` | Saves `playwright/.auth/frontend.json` after login |
| `public-auth.spec.ts` | Frontend — public auth | Landing links; login to `/app`; register; forgot-password |
| `app-shell.spec.ts` | Frontend — authenticated app shell | Dashboard/profile nav; 2FA setup page; media lab page (uses `storageState`) |
| `support.spec.ts` | Frontend — support center | Create support ticket; search by ticket number; reply flow |

**Auth pattern:** tokens in **localStorage** → `storageState` works for authenticated project.

---

## Playwright — Admin

**Folder:** `e2e/tests/admin/`  
**Base URL:** `E2E_ADMIN_URL` (default `http://localhost:5174`)

| File | Describe block | Coverage |
|------|----------------|----------|
| `public-auth.spec.ts` | Admin console — public auth | Login page; login reaches dashboard |
| `navigation.spec.ts` | Admin console — navigation | Dashboard metrics; audit logs + DLQ navigation |
| `settings.spec.ts` | Admin console — settings | Users list → settings; public-register toggle; backup controls |
| `support.spec.ts` | Admin console — support tickets | Support route, search/filter controls, empty-state detail panel |

**Auth pattern:** `accessToken` in **Zustand memory** → each authenticated test uses `beforeEach(loginAdminConsole())`; **no** `storageState` project.

---

## Shared Playwright helpers

| File | Purpose |
|------|---------|
| `e2e/tests/helpers/login.ts` | `loginFrontend`, `loginAdminConsole`; waits for `POST /api/v1/auth/login` |

---

## Mapping to product areas

| Product area | API integration | Playwright |
|--------------|-----------------|------------|
| Platform health | Yes | Implicit (webServer health) |
| Auth (register, verify, login, refresh, password) | Yes | Partial (login, register/forgot UI) |
| User profile | Yes | Profile navigation |
| 2FA | Via API flows | Setup page smoke |
| Notifications (in-app + realtime hook) | Yes (`NotificationsController`, query/mark-read) | Implicit by authenticated app requests in frontend suite |
| Support tickets | Yes (`/support/tickets`, `/admin/support/tickets`) | Yes (frontend create/search/reply + admin support page controls) |
| Admin users / settings / audit | Yes | Users, settings (including backup controls), dashboard, audit navigation |
| Media pipeline | Optional (MinIO) | Media lab page smoke |
| Messaging / DLQ | Admin test (Rabbit off) | — |

Gaps are intentional smoke-level coverage; extend per [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md).

---

## Related feature catalogs

When asserting behavior, cross-check endpoint and UI contracts:

- [`docs/backend/FEATURE_CATALOG.md`](../backend/FEATURE_CATALOG.md)
- [`docs/frontend/FEATURE_CATALOG.md`](../frontend/FEATURE_CATALOG.md)
- [`docs/admin/FEATURE_CATALOG.md`](../admin/FEATURE_CATALOG.md)
