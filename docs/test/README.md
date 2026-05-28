# Testing documentation

Automated tests for the Base Project monorepo: **API integration** (xUnit + `WebApplicationFactory`) and **browser E2E** (Playwright for Frontend + Admin).

| Layer | Source | Docs |
|-------|--------|------|
| API integration | [`src/Backend/Tests/Api.IntegrationTests`](../../src/Backend/Tests/Api.IntegrationTests/) | [TEST_CATALOG.md](./TEST_CATALOG.md) § API |
| Playwright UI | [`e2e/`](../../e2e/) | [TEST_CATALOG.md](./TEST_CATALOG.md) § Playwright |
| Run scripts | [`tools/e2e/`](../../tools/e2e/) | [ONBOARDING.md](./ONBOARDING.md) |

Stack prerequisites: **Docker**, **.NET 8**, **Node.js 20+**.

Latest CI matrix:
- `backend`: unit + integration tests
- `backend-media`: MinIO-backed media integration (`INTEGRATION_MEDIA_ENABLED=true`)
- `playwright`: full UI e2e against isolated `baseproject_e2e`

---

## Quick entry points

| You are… | Read this first |
|----------|-----------------|
| New engineer running tests locally | [`ONBOARDING.md`](./ONBOARDING.md) |
| PM / tech lead — what is covered today | [`TEST_CATALOG.md`](./TEST_CATALOG.md) |
| Engineer adding or fixing tests | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) |
| AI assistant changing tests or e2e | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) |
| Cursor rule file | [`.cursor/rules/test.mdc`](../../.cursor/rules/test.mdc) |

---

## Documentation map

| Document | Purpose |
|----------|---------|
| [ONBOARDING.md](./ONBOARDING.md) | Prerequisites, run commands, env vars, troubleshooting |
| [TEST_CATALOG.md](./TEST_CATALOG.md) | Inventory of integration + Playwright specs |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Fixtures, factories, Playwright projects, auth patterns |
| [EXTENSIBILITY_GUIDE.md](./EXTENSIBILITY_GUIDE.md) | How to add API or UI tests when shipping features |
| [LLM_FEATURE_IMPLEMENTATION_RULES.md](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Strict rules for Cursor / AI agents |

---

## Related docs

| Area | Location |
|------|----------|
| Backend API behavior | [`docs/backend/`](../backend/README.md) |
| DataProtection keys (login failures) | [`docs/backend/DATA_PROTECTION.md`](../backend/DATA_PROTECTION.md) |
| Frontend auth / proxy | [`docs/frontend/ARCHITECTURE.md`](../frontend/ARCHITECTURE.md) |
| Admin auth (Zustand token) | [`docs/admin/ARCHITECTURE.md`](../admin/ARCHITECTURE.md) |
| Local Docker stack | [`local/README.md`](../../local/README.md) |
| Short e2e README (commands) | [`e2e/README.md`](../../e2e/README.md) |

---

## Out of scope (by design)

- Load / performance testing
- Visual regression (no Percy/Chromatic setup)
- Mobile native apps
- Production environment smoke tests (use isolated Postgres + ephemeral keys only)

Tests target **Development** configuration with seeded `admin@localhost.com` / `Temp!ChangeMe123` unless overridden by env vars.
