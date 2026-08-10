---
name: repo-conventions
description: Load this repo's mandatory conventions before editing src/Backend (.NET 8), src/Admin (Vite/React), src/Frontend (Next.js), deploy/, or e2e/ & Backend Tests. Mirrors .cursor/rules/*.mdc — use whenever a task touches one of these areas, or when unsure which layer/doc to consult in this codebase.
---

# Repo conventions (derived from `.cursor/rules/*.mdc`)

This project encodes area-specific rules as Cursor `.mdc` files under `.cursor/rules/`,
each scoped by a `globs:` pattern. Claude Code doesn't auto-apply glob-scoped rules, so
this skill is the substitute: **before editing files in one of the areas below, re-read
the matching `.mdc` file (they're short, ~50-120 lines) and the docs it points to.**

| Area | Glob | Rule file |
|------|------|-----------|
| Backend (.NET 8) | `src/Backend/**/*` | `.cursor/rules/backend.mdc` |
| Admin console (Vite/React) | `src/Admin/**/*` | `.cursor/rules/admin.mdc` |
| Frontend (Next.js) | `src/Frontend/**/*` | `.cursor/rules/frontend.mdc` |
| Deploy (Docker/Traefik/CI) | `deploy/**/*`, `docs/deploy/**/*`, `.github/workflows/**/*`, `tools/**/*deploy*` | `.cursor/rules/deploy.mdc` |
| Tests (xUnit + Playwright) | `e2e/**/*`, `src/Backend/Tests/**/*`, `tools/e2e/**/*` | `.cursor/rules/test.mdc` |

## Quick-reference cheat sheet (full detail lives in the .mdc + docs)

### Backend (`src/Backend`) — hub: `docs/backend/README.md`
- Layering is non-negotiable: **Domains** (no Infra/Api/EF refs) → **Application** (CQRS handlers, depend only on `Application/Contracts`) → **Infrastructure** (EF/messaging/email/storage) → **Api** (thin, maps to MediatR, `ApiResponse<T>`, no business rules) → **Worker** (cron + queue consumers, sole outbox drainer, no `[Authorize]`).
- Queries use `IApplicationReadDbContext`/`AsNoTracking`; commands use tracked entities + one `SaveChangesAsync` via `IUnitOfWork`. Never mutate read-context entities.
- EF LINQ: compare `u.Id == UserId.Create(id)`, never `u.Id.Value == id` (fails translation).
- Domain events: `ITransactionalDomainEvent` (in-process) / `IAfterCommitDomainEvent` (post-commit) / `IIntegrationEvent` (outbox → RabbitMQ, Api never registers `AddOutboxPublisher`).
- Done criteria: `dotnet build src/Backend/Backend.sln`, add/update tests, update `FEATURE_CATALOG.md`, minimal diff, no `DbContext` in controllers.

### Admin (`src/Admin`) — hub: `docs/admin/ONBOARDING.md`
- `features/*` = pages only, may import `shared/*`; `shared/*` must NOT import `features/*`.
- New UI strings go through `useTranslation()`/`t()` + keys added to `en.json`, `vi.json`, `zh.json` together.
- Reuse `shared/lib` (`formatDateOrDash`, `toast`, `runAdminAction`, `queryKeys`, `usePagination`, ...) — never `toLocaleDateString()`.
- After changes: `npx tsc -b` in `src/Admin`; update `docs/admin/FEATURE_CATALOG.md` for new user-facing features.

### Frontend (`src/Frontend`) — hub: `docs/frontend/ONBOARDING.md`
- `app/*` stays thin (routes only); logic lives in `lib/features/*` + hooks. `lib/shared/*` must NOT import `app/*`/`components/*`.
- Dev API calls go through `/api/v1/...` (proxied), never direct `https://localhost:7124`.
- i18n and date-formatting rules mirror Admin (3 locale files, no `toLocaleDateString()`).
- After changes: `npm run typecheck` in `src/Frontend`; update `docs/frontend/FEATURE_CATALOG.md`.

### Deploy (`deploy/`, workflows) — hub: `docs/deploy/README.md`
- Everything runs on Docker behind Traefik; Api and Worker are separate services; Worker stays private (no public route).
- Prefer same-origin `/api` routing over cross-origin API domains.
- Env files split by environment; commit only `.env.example`, never real secrets.
- Migration runs before rollout; health checks (`/health/live`, `/health/ready`) gate releases; never bypass tests/migration for convenience.

### Tests (`e2e/`, `src/Backend/Tests`) — hub: `docs/test/README.md`
- Two layers: xUnit API integration (`src/Backend/Tests/Api.IntegrationTests`) and Playwright e2e (`e2e/tests/`).
- Playwright auth: Frontend uses `storageState` from `auth.setup.ts`; Admin uses `beforeEach(loginAdminConsole())` — no `storageState` (JWT lives in Zustand memory), don't mix these up.
- Default ports: API `:5076` (`E2E_API_URL`, avoid `:5000` on macOS/AirPlay), FE `:3000`, Admin `:5174`. Seed creds: `admin@localhost.com` / `Temp!ChangeMe123`.
- A login 500 / `EncryptedColumnDecryptionException` means DataProtection key mismatch, not CORS.

## Cross-cutting habits worth carrying into any area
- Minimal diff — no unrelated refactors, no speculative abstractions.
- No secrets committed anywhere (env files, DataProtection keys, tokens).
- Every area has an `LLM_FEATURE_IMPLEMENTATION_RULES.md` under its `docs/<area>/` folder — read it for the full AI-facing checklist beyond this summary before larger changes.
