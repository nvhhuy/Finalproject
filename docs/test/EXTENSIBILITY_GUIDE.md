# Extensibility guide — adding tests

When you ship a **new API endpoint**, **admin screen**, or **frontend route**, extend tests in the smallest layer that proves the behavior.

---

## Decision tree

```
New behavior?
├── Pure domain / handler logic, no HTTP
│   └── Add or extend unit test in src/Backend/Tests (not Api.IntegrationTests)
├── HTTP contract (auth, status codes, persistence)
│   └── Api.IntegrationTests (+ update TEST_CATALOG.md)
└── User-visible navigation or form
    └── Playwright smoke in e2e/tests/ (+ update TEST_CATALOG.md)
```

Prefer **API integration** for business rules; use **Playwright** only for routing, layout, and critical click paths.

---

## Adding an API integration test

### 1. Pick the test class

| Domain | File |
|--------|------|
| Auth / register / password | `AuthFlowIntegrationTests.cs` or `AuthIntegrationTests.cs` |
| Profile | `UsersIntegrationTests.cs` |
| Admin | `AdminIntegrationTests.cs` |
| Health | `PlatformIntegrationTests.cs` |
| Media (MinIO) | `MediaIntegrationTests.cs` |

### 2. Use existing helpers

```csharp
// IntegrationTestApp from fixture
var app = _fixture.App;
var (auth, email) = await app.RegisterVerifyAndLoginAsync();
var token = auth.AccessToken;
```

- Email links: `factory.EmailSink` + `EmailBodyParser`
- HTTP: `client.PostAsJsonAsync` via extensions in `IntegrationHttpExtensions`

### 3. Factory / config

- Do **not** point integration factory at production connection strings
- New feature flags → add to `CustomWebApplicationFactory` in-memory config if needed
- Media or Rabbit-dependent tests → gate with env var (see `MediaIntegrationTests`)

### 4. Done criteria

- [ ] `dotnet test src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj` passes
- [ ] Entry added to [`TEST_CATALOG.md`](./TEST_CATALOG.md)
- [ ] If new public endpoint, update [`docs/backend/FEATURE_CATALOG.md`](../backend/FEATURE_CATALOG.md)

---

## Adding a Playwright test

### 1. Choose project

| App | Directory | Auth |
|-----|-----------|------|
| Frontend | `e2e/tests/frontend/*.spec.ts` | Public: no setup; Authenticated: `storageState` from `auth.setup.ts` |
| Admin | `e2e/tests/admin/*.spec.ts` | Public: none; Authenticated: `beforeEach(loginAdminConsole())` |

### 2. Reuse login helpers

```ts
import { loginFrontend, loginAdminConsole } from '../helpers/login';
```

Do not duplicate selectors across specs — extend `login.ts` if login UI changes.

### 3. Selectors

- Prefer `getByRole`, `getByLabel`, stable `id` attributes (`#email`)
- Avoid brittle CSS chains and `data-testid` unless the app already exposes them

### 4. Waiting

- Wait for network: login helper already waits on `POST /api/v1/auth/login`
- Navigation: `expect(page).toHaveURL(...)` with reasonable timeout
- Do not use fixed `page.waitForTimeout` except as last resort

### 5. Done criteria

- [ ] `cd e2e && npm test` (or targeted `npm run test:frontend` / `test:admin`)
- [ ] Entry in [`TEST_CATALOG.md`](./TEST_CATALOG.md)
- [ ] Update relevant [`FEATURE_CATALOG.md`](../frontend/FEATURE_CATALOG.md) only if you added product features, not for test-only tweaks

---

## Environment-specific tests

| Need | Approach |
|------|----------|
| MinIO / media | `INTEGRATION_MEDIA_ENABLED=true` + running MinIO from `local/docker-compose.yml` |
| Rabbit / DLQ UI | Keep Rabbit disabled in default e2e; assert empty/disabled state or use API integration |
| Skip slow UI | Mark with `test.skip` + document env in catalog |

---

## CI checklist (when wiring pipeline)

1. Service: Postgres 16 + run migrations
2. Job 1: `dotnet test Api.IntegrationTests` (Docker-in-Docker or Testcontainers)
3. Job 2: `cd e2e && npm ci && npx playwright install --with-deps && npm test`
4. Upload `e2e/playwright-report` on failure
5. Never use production secrets or shared DP key directories

---

## Anti-patterns

- Duplicating full admin CRUD in Playwright when API integration already covers it
- Using `storageState` for Admin authenticated flows
- Committing `e2e/test-results/` or `playwright-report/` (gitignored)
- Hard-coding production URLs or real user passwords
- Tests that depend on execution order without xUnit collection/fixture discipline
