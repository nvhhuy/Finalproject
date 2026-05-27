# Backend docs hub

Use this page as the table of contents for all backend documentation.

## By goal

### 1) I am new and need to run the project quickly

1. [`ONBOARDING.md`](./ONBOARDING.md)
2. [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md)
3. [`ARCHITECTURE.md`](./ARCHITECTURE.md)
4. [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md)

### 2) I need to know what features are already available

1. [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) (full catalog + endpoints)
2. [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md) (short status matrix)
3. [`CODEBASE_INCLUDED_AND_USAGE_GUIDE.md`](./CODEBASE_INCLUDED_AND_USAGE_GUIDE.md)

### 3) I need to add a new backend feature

1. [`BASE_GUIDE.md`](./BASE_GUIDE.md)
2. [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md)
3. [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
4. [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md)

### 4) I need to modify DB schema / migrations safely

1. [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md)
2. [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) (if your CI runs on Gitea Actions)

---

## By topic

| Topic | Doc |
|---|---|
| Architecture boundaries | [`ARCHITECTURE.md`](./ARCHITECTURE.md) |
| Project/module map | [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md) |
| Feature implementation flow | [`BASE_GUIDE.md`](./BASE_GUIDE.md) |
| Production hardening gaps checklist | [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md) |
| Caching model | [`CACHE_GUIDE.md`](./CACHE_GUIDE.md) |
| Feature inventory | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Legacy baseline matrix | [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md) |
| Migration guardrails | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) |
| Gitea CI setup | [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) |
| Coding style references | [`STYLE_FROM_REFERENCE_PROJECTS.md`](./STYLE_FROM_REFERENCE_PROJECTS.md) |

---

## Suggested maintenance rule

Whenever a backend feature is added/changed:

1. Update [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md).
2. If onboarding impact exists, update [`ONBOARDING.md`](./ONBOARDING.md).
3. If migration/CI behavior changes, update [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) and/or [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md).
