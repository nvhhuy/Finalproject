# LLM / AI rules — tests and e2e

Strict guide for Cursor and other AI assistants when changing **`src/Backend/Tests/**`**, **`e2e/**`**, or **`tools/e2e/**`**.

Human developers should follow the same rules where practical.

---

## Scope

| In scope | Out of scope (use other LLM docs) |
|----------|----------------------------------|
| `src/Backend/Tests/**` | `src/Backend/Api` product code → `docs/backend/LLM_*` |
| `e2e/**` | `src/Frontend/**` → `docs/frontend/LLM_*` |
| `tools/e2e/**` | `src/Admin/**` → `docs/admin/LLM_*` |

**Docs:** update `docs/test/` when adding tests, changing run prerequisites, or new auth/port patterns.

---

## Non-negotiable rules

1. **Minimal diff**
   - Change only tests and test infra required for the task
   - No unrelated refactors in app code unless the user asked

2. **Two layers — pick correctly**
   - HTTP / persistence / auth tokens → **Api.IntegrationTests**
   - Browser navigation / visible UI → **Playwright**
   - Do not duplicate full admin CRUD in both layers

3. **Admin vs Frontend auth in Playwright**
   - Frontend authenticated: `storageState` from `auth.setup.ts` is OK
   - Admin authenticated: **`loginAdminConsole()` in `beforeEach`** — never rely on `storageState` for JWT in Zustand

4. **DataProtection + Postgres**
   - Playwright API must use `local/dataprotection-keys` (already in `playwright.config.ts`)
   - Do not point tests at production DB or production key paths
   - If login fails with decryption errors, fix **DB reset / keys**, not CORS or proxy hacks

5. **Ports**
   - Default e2e API: **`5076`** (not `5000` on macOS)
   - Keep `E2E_API_URL`, `API_PROXY_TARGET`, and health check URLs aligned

6. **Secrets**
   - Use seed credentials `admin@localhost.com` / `Temp!ChangeMe123` or env overrides documented in ONBOARDING
   - Never commit real passwords, JWT secrets, or `.env` with secrets

7. **Catalog updates**
   - New test class or major scenario → update `docs/test/TEST_CATALOG.md`
   - New product feature under test → update the relevant `FEATURE_CATALOG.md` in backend/frontend/admin

8. **Verification**
   - API: `dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj`
   - UI: `cd e2e && npm test` (or scoped `test:frontend` / `test:admin`)
   - Report skips (e.g. media) and env vars required

---

## API integration patterns

```csharp
// GOOD — use fixture app + helpers
var (auth, _) = await _fixture.App.RegisterVerifyAndLoginAsync();
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", auth.AccessToken);

// BAD — raw SQL in tests, production connection string, skipping SaveChanges assumptions
```

- Register email verification via `CapturingEmailSender`, not external SMTP
- One `SaveChanges` per command is backend rule — tests assert HTTP outcomes, not bypass UoW
- Gate optional infra (MinIO, Rabbit) with explicit env flags

---

## Playwright patterns

```ts
// GOOD — shared helper + API wait
import { loginAdminConsole } from '../helpers/login';
test.beforeEach(async ({ page }) => {
  await loginAdminConsole(page);
});

// BAD — storageState for Admin JWT
test.use({ storageState: 'playwright/.auth/admin.json' });
```

- Extend `e2e/tests/helpers/login.ts` when login form changes
- Use `getByRole` / `getByLabel` over XPath
- Surface API failures: keep `waitForResponse` on login POST

---

## File placement

| What | Where |
|------|-------|
| New API scenario | `Api.IntegrationTests/<Domain>IntegrationTests.cs` |
| HTTP / email helpers | `Api.IntegrationTests/Support/` |
| Factory / fixture | `CustomWebApplicationFactory.cs`, `IntegrationAppFixture.cs`, `PostgresFixture.cs` |
| Frontend UI spec | `e2e/tests/frontend/<name>.spec.ts` |
| Admin UI spec | `e2e/tests/admin/<name>.spec.ts` |
| Shared browser helpers | `e2e/tests/helpers/` |
| Run scripts | `tools/e2e/*.sh` |

---

## Forbidden

- `storageState` for Admin authenticated suites
- Disabling `global-setup` DB reset without documenting alternative
- `page.waitForTimeout` as primary synchronization
- Tests depending on manual dev server on wrong port
- Committing Playwright traces/reports (`test-results/`, `playwright-report/`)
- Changing production `appsettings` for test convenience

---

## Done criteria (AI)

1. Tests pass locally or failure reason is documented (Docker down, MinIO skipped, etc.)
2. `docs/test/TEST_CATALOG.md` updated if inventory changed
3. No secrets in diff
4. ONBOARDING or ARCHITECTURE updated if ports, env vars, or auth model changed
