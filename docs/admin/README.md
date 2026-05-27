# Admin Console documentation

Internal administrator UI for the Final Project backend. Source: [`src/Admin/`](../../src/Admin/).

Stack: **Vite 8 · React 19 · TypeScript · TanStack Query · Zustand · i18next · Tailwind CSS 4**.

---

## Quick entry points

| You are… | Read this first |
|----------|-----------------|
| New Admin developer | [`ONBOARDING.md`](./ONBOARDING.md) |
| PM / tech lead — what exists today | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Engineer adding a screen or flow | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) |
| Engineer using shared helpers | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) |
| AI assistant implementing Admin UI | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) |

---

## Documentation map

| Document | Purpose |
|----------|---------|
| [ONBOARDING.md](./ONBOARDING.md) | Day 1 setup, env, run dev, reading order |
| [FEATURE_CATALOG.md](./FEATURE_CATALOG.md) | Routes, features, API surface, i18n |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Folders, data flow, auth, state |
| [COMMON_LIBRARIES.md](./COMMON_LIBRARIES.md) | `shared/lib`, hooks, patterns, query keys |
| [EXTENSIBILITY_GUIDE.md](./EXTENSIBILITY_GUIDE.md) | Step-by-step: new page, API, i18n |
| [LLM_FEATURE_IMPLEMENTATION_RULES.md](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Strict rules for Cursor / AI agents |

---

## Related docs

| Area | Location |
|------|----------|
| Backend API (admin endpoints) | [`docs/backend/`](../backend/README.md) |
| Optimistic concurrency (`rowVersion`) | [`docs/backend/OPTIMISTIC_CONCURRENCY.md`](../backend/OPTIMISTIC_CONCURRENCY.md) |
| Public user frontend | [`docs/frontend/`](../frontend/README.md) |
| Local Docker stack | [`local/README.md`](../../local/README.md) |

---

## Out of scope (by design)

- Public user registration UI (lives in `src/Frontend`)
- Mobile-native admin app
- Offline / PWA (not implemented)

Admin is a **browser SPA** for operators with JWT role **Admin** only.
