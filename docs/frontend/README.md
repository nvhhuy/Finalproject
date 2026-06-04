# Frontend documentation

Public end-user web app (Next.js App Router). Source: [`src/Frontend/`](../../src/Frontend/).

Stack: **Next.js 15 · React 19 · TypeScript · TanStack Query · react-i18next · Tailwind CSS 4**.

Designed as a **reusable base**: landing + auth + profile + media lab; add product-specific routes on top.

**Klassi branding (v4):** Duolingo-inspired green on light mint background — see [`docs/business/DESIGN_GUIDE.md`](../business/DESIGN_GUIDE.md). Admin console uses a separate blue operator theme.

---

## Quick entry points

| You are… | Read this first |
|----------|-----------------|
| New Frontend developer | [`ONBOARDING.md`](./ONBOARDING.md) |
| PM / tech lead — what exists today | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Engineer adding a page or API client | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) |
| Engineer using shared helpers | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) |
| AI assistant implementing Frontend | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) |

---

## Documentation map

| Document | Purpose |
|----------|---------|
| [ONBOARDING.md](./ONBOARDING.md) | Day 1 setup, env, run dev, reading order |
| [FEATURE_CATALOG.md](./FEATURE_CATALOG.md) | Routes, features, API surface, i18n |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Folders, data flow, auth, API proxy |
| [COMMON_LIBRARIES.md](./COMMON_LIBRARIES.md) | `lib/shared`, format, errors |
| [EXTENSIBILITY_GUIDE.md](./EXTENSIBILITY_GUIDE.md) | Step-by-step: new page, feature API, i18n |
| [LLM_FEATURE_IMPLEMENTATION_RULES.md](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Strict rules for Cursor / AI agents |

---

## Related docs

| Area | Location |
|------|----------|
| Backend API | [`docs/backend/`](../backend/README.md) |
| Admin Console (operators) | [`docs/admin/`](../admin/README.md) |
| Optimistic concurrency (`rowVersion`) | [`docs/backend/OPTIMISTIC_CONCURRENCY.md`](../backend/OPTIMISTIC_CONCURRENCY.md) |
| Local Docker stack | [`local/README.md`](../../local/README.md) |

---

## Out of scope (by design)

| Capability | Where instead |
|------------|----------------|
| Admin user management, audit, DLQ | [`src/Admin/`](../../src/Admin/) |
| Backend business rules | [`src/Backend/`](../../src/Backend/) |
| Server-side avatar field on profile | Not in API yet — media upload is a demo flow |

Frontend targets **authenticated end users** (JWT), not Admin role operations.
