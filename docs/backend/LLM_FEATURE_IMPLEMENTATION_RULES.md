# LLM Feature Implementation Rules (Strict)

Purpose: this file is a strict guide for AI/code assistants when implementing new backend features in this repository.

Scope: `src/Backend/` only.

**Cursor:** `.cursor/rules/backend.mdc` mirrors these rules for `src/Backend/**/*`.

**Doc hub:** [`README.md`](./README.md).

---

## Non-negotiable architecture rules

1. **Do not place business rules in controllers.**
   - Controllers only handle HTTP concerns and call Application layer.
2. **Do not let Domain depend on Infrastructure or Api.**
3. **All new feature code must follow module paths:**
   - `Domains/Modules/<Feature>/...`
   - `Application/Contracts/Modules/<Feature>/...`
   - `Application/Modules/<Feature>/...`
   - `Infrastructure/Modules/<Feature>/...`
   - `Api/Controllers/<Feature>/...` and `Api/Models/...`
4. **Keep API response envelope consistent** via `ApiResponse<T>` patterns.

---

## Implementation pattern rules

### A) Commands vs queries

- Query handlers use read-side abstractions/repositories (`I...ReadRepository`).
- Command handlers use write-side abstractions/repositories (`I...WriteRepository` or `ForUpdate` methods).
- Do not mutate entities loaded from read context.

### B) Repository reuse

- Reuse generic methods in repository bases:
  - `CachedReadRepositoryBase` helpers (`ExistsAsync`, cached predicate loaders, etc.).
  - `WriteRepositoryBase` helpers (tracked-for-update, add helpers, cache invalidation helpers).
- Avoid re-implementing identical EF query boilerplate in each repository.
- Keep domain-specific query logic in concrete repositories.

### C) Transactions

- For API mutation endpoints requiring atomicity, use `[Transactional]` or explicit `IUnitOfWork` flow (not both unless required).
- Any write path that affects cached read models must invalidate relevant cache keys.

### D) Domain events

- Use `ITransactionalDomainEvent` for in-transaction reactions.
- Use `IAfterCommitDomainEvent` for after-commit side effects.
- Do not bypass existing event dispatch flow/interceptors.

### E) Worker patterns

- Cron jobs: implement `ICronJob`.
- Queue consumers: inherit `QueueConsumerBackgroundService<TMessage>`.
- Keep broker-specific details in Infrastructure adapters, not in Application logic.
- **Only Worker** registers `AddOutboxPublisher` — never on Api.
- Worker has **no** JWT/`[Authorize]` — see [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md).

### F) EF LINQ (mandatory)

- Predicates on value-object IDs: `u.Id == UserId.Create(id)` — **never** `u.Id.Value == id` in `IQueryable`.
- No `DateOnly.FromDateTime` inside SQL-translated `GroupBy`; group by `DateTime.Date`, project in memory.
- Read context: no `SaveChanges`; writes use tracked entities + `IUnitOfWork`.
- See [`EF_PITFALLS.md`](./EF_PITFALLS.md).

### G) DataProtection

- Do not commit `local/dataprotection-keys/`.
- Production requires persistent `DataProtection:KeysDirectory` shared across Api replicas (and Worker if touching encrypted columns).
- See [`DATA_PROTECTION.md`](./DATA_PROTECTION.md).

---

## Security and reliability rules

1. Never commit real secrets.
2. Preserve JWT validation strictness and role policy patterns.
3. Add authorization attributes/policies for protected endpoints.
4. Preserve correlation/logging context in new flows where applicable.
5. Add input validation for commands/requests (validators or explicit guard checks).
6. Prefer idempotent handlers for externally triggered operations.

---

## Naming and structure conventions

- Use feature-first folder naming under `Modules/<Feature>/`.
- Use explicit suffixes:
  - `...Command`, `...Query`, `...Handler`, `...Validator`
  - `...Repository` / `...ReadRepository` / `...WriteRepository`
  - `...Controller`, optionally split into partial `...Read.cs` and `...Write.cs`.
- Keep DTO/API contracts in `Api/Models/...`, not Domain entities.

---

## Pull request / change quality rules for LLM

When adding a feature, assistant must also:

1. Update relevant backend docs in `docs/backend/` (at least one file — see [`README.md`](./README.md) maintenance table).
2. Ensure project builds (`dotnet build src/Backend/Backend.sln`).
3. Run or add tests for handlers/validators/non-trivial EF queries.
4. Avoid introducing unrelated refactors.
5. Preserve backward-compatible API behavior unless explicitly asked to break/change it.
6. Keep `.cursor/rules/backend.mdc` in sync if conventions change.

---

## Forbidden shortcuts

- Do not add direct DbContext usage in controllers.
- Do not move business decisions into Infrastructure services.
- Do not couple queue consumer code directly to HTTP or controller types.
- Do not bypass cache invalidation when write operations affect cached keys.
- Do not mix dev-only config assumptions into production logic.

---

## Done criteria for feature implementation

A feature is only "done" when:

1. Domain, application, infrastructure, and API layers are aligned.
2. Security and auth checks are included where required.
3. Cache/transaction/event semantics are correct.
4. Worker behavior is deterministic for background/queue flows.
5. Documentation is updated to describe usage and extension points.
