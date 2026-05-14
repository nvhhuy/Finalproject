# Backend base guide

This document explains how to use the shared infrastructure in this solution: layers, persistence, transactions, domain events, auth, admin operations, app settings, media, caching, messaging hooks, scheduling, and typical controller patterns.

For onboarding topics (JWT vs policies, DTO mapping choices, Worker triggers, observability, testing strategy), see **`DEVELOPER_GUIDE.md`**.

---

## 1. Solution layout

| Project | Role |
|---------|------|
| **Domains** | `Common/` + **`Modules/{Domain}/`** (e.g. `Modules/Users/UserAggregate`). See **`MODULE_STRUCTURE.md`**. For how this lines up with your **Books** and **FaceBookClone** reference repos (handlers, slices, API), see **`STYLE_FROM_REFERENCE_PROJECTS.md`**. |
| **Application** | **`Modules/{Domain}/`** for feature code + **`Contracts/Modules/{Domain}/`** for that domain’s ports; global `Contracts/` for cross-cutting ports. |
| **Infrastructure** | Shared `Persistence/` + **`Modules/{Domain}/`** for domain-specific EF repos/config (e.g. Users). |
| **Api** | HTTP API, filters, authorization policies, controllers. |
| **Worker** | Background host (cron jobs and queue-consumer workers). |
| **Shared** | Cross-cutting types (e.g. `Result`, errors). |

Registration flow: **Api** `Program.cs` calls `AddApplication()` then `AddInfrastructure(configuration)`.

See **`ARCHITECTURE.md`** for DDD boundaries and the **`ApiResponse<T>`** contract.

---

## 2. API response envelope (`ApiResponse<T>`)

Successful and failed JSON bodies share one shape (`success`, `data`, `error`, `meta`). Helpers live on **`ApiControllerBase`** (`OkApi`, `OkApiEmpty`, `MatchApi`, `FailApi`, `BadRequestValidationApi`).

Request/response DTOs used only at the HTTP edge live under **`Api/Models/`** (e.g. `Api/Models/Auth`, `Api/Models/Admin`, `Api/Models/Users`).

---

## 3. Databases (read / write)

- **Write:** `ApplicationDbContext` — connection string `DatabaseConnectionString`.
- **Read:** `ApplicationReadDbContext` — `DatabaseReadConnectionString`; if empty, falls back to the write string (typical in local dev).
- **`IDatabaseTopology`** — describes whether read and write point at the same normalized endpoint (same physical DB vs dedicated read endpoint). One SQL transaction cannot span two PostgreSQL servers; use outbox or sagas for cross-database consistency.
- **`AppSettings` table** — stores runtime feature toggles and key/value controls (example: `feature.auth.allow_public_register`).

**Repositories**

- **`IUserReadRepository`** — cached reads, no-tracking queries, paging.
- **`IUserWriteRepository`** — tracked writes (`SaveChanges` on write context).
- **`IUserRespository`** — facade over read + write for existing handlers.
- **Repository bases** (`Infrastructure/Persistence/Repositories/Base`) expose reusable generic methods (`ExistsAsync`, cached predicate loaders, tracked-for-update helpers) to reduce boilerplate in future repositories.

**Important for commands that change users**

- Use **`IUserRespository.GetUserByIdForUpdateAsync`** (or **`IUserWriteRepository.GetByIdForUpdateAsync`**) so EF tracks the entity on the **write** context before you mutate and call `SaveChangesAsync`.
- Use **`GetUserByIdAsync`** only when you will not persist changes to that entity instance.

---

## 4. Transactions

### 4.1 Declarative: `[Transactional]`

- Attribute: `Api/Filters/TransactionalAttribute.cs` (backed by `TransactionalActionFilter`).
- Register filter in DI: `services.AddScoped<TransactionalActionFilter>();` (already in `Api/Program.cs`).
- Apply on a **controller** or **action** that runs commands using the **scoped write** `ApplicationDbContext`.

**Behavior**

- Begins an EF Core transaction before the action.
- After the action: **commits** if the HTTP status is **&lt; 400**; **rolls back** on unhandled exceptions or status **≥ 400** (so “business failure” responses still roll back the DB work in that request).

**Example in this repo**

- `AuthController` — `[Transactional]` on **register** so user insert + downstream work stay consistent with rollback on failure.
- **`AdminUsersController`** / **`UsersController`** — **`[Transactional]`** only on **mutating** actions (not on GET/list).

### 4.2 Manual: `IUnitOfWork`

- Interface: `Application/Contracts/Transactions/IUnitOfWork.cs`.
- Implementation: `Infrastructure/Persistence/EfUnitOfWork.cs`.

Typical pattern inside a controller action:

```csharp
await _unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    // await _mediator.Send(...); // handlers call SaveChanges as needed
    await _unitOfWork.CommitAsync(cancellationToken);
}
catch
{
    await _unitOfWork.RollbackAsync(cancellationToken);
    throw;
}
```

**Example:** `SamplesController` (partial) — `POST api/samples/transactions/manual-lock/{userId}`.

**Do not** stack `[Transactional]` and manual `Begin` on the same action without a clear reason; prefer one approach per request.

---

## 5. Domain events (in transaction vs after commit)

**Markers (Domains)**

- **`ITransactionalDomainEvent`** — handled **after `SaveChanges`**, while an ambient EF transaction is still open (or immediately after save if there is no open transaction).
- **`IAfterCommitDomainEvent`** — if a transaction is open, notifications are **queued** until `IUnitOfWork.CommitAsync` runs; if there is no transaction, they are published right after save.

**Raising events**

- Aggregates inherit `Entity<T>` / `AggregateRoot<T>` with **`AddDomainEvent(IDomainEvent)`**.
- For MediatR handlers, define the event as **`IDomainEvent` + `INotification`** (see `Application/Modules/Users/Profile/DomainEvents/`).

**Dispatch**

- `DomainEventsSaveChangesInterceptor` runs after successful `SaveChanges`, publishes transactional events, queues or publishes after-commit events.
- `EfUnitOfWork.CommitAsync` drains the after-commit queue after the DB commit.

**Example**

- Registration raises `UserRegisteredInTransactionEvent` and `UserRegisteredAfterCommitEvent` in `RegisterCommandHandler`.

---

## 6. Authentication and authorization

- JWT is configured in `Infrastructure/DependencyInjection.cs` (`AddAuth`).
- **`[JwtAuthorize]`** (`Api/Authorization/JwtAuthorizeAttribute.cs`) pins the **JWT bearer** authentication scheme (`AuthenticationSchemes`). It is **optional** while JWT is the only default scheme—**`[Authorize]`** / **`[Authorize(Policy = ...)]`** is enough. Use `[JwtAuthorize]` when you add multiple auth schemes or want an explicit JWT-only marker; combine with **`[Authorize]`** or **`[Authorize(Policy = ...)]`** for who may access the resource. See **`DEVELOPER_GUIDE.md`** §2 (auth) and **§9** (admin platform: audit, email, metrics, sessions).
- Admin-only policy: **`AuthorizationPolicies.AdminOnly`** (requires role `UserRoles.Admin`).

**Controllers (partial classes, CQRS-friendly routes)**

- **`AdminUsersController`** — `AdminUsersController.cs` + `.Read.cs` + `.Write.cs`  
  - `GET api/admin/users` — paged list with optional filters (`emailContains`, `userNameContains`, `isLocked`, `role`, `sortBy`, `sortDescending`)  
  - `POST api/admin/users` — create user; `PUT/DELETE .../{id}` — update / soft-delete  
  - `POST .../{id}/lock|unlock|impersonate|2fa/reset|password/reset` — mutating commands (`[Transactional]`)  
  - `POST .../{id}/email/verify` — admin marks email verified; `POST .../{id}/email/resend-verification` — sends verification link (cache + `IEmailSender`); `POST .../{id}/sessions/revoke` — revokes all refresh tokens for that user  
- **`AdminMetricsController`** — `GET api/admin/metrics/dashboard` — total active users + daily registration counts (last 14 days, uses `RegisteredAtUtc`)  
- **`AdminAuditLogsController`** — `GET api/admin/audit-logs` — paged audit trail (`targetUserId` optional filter)  
- **`UsersController`** — `.Read.cs` (e.g. `GET api/users/profile`), `.Write.cs` (`PUT api/users/profile` + `[Transactional]`)
- **`SamplesController`** — transaction demos under `api/samples/transactions/...`
- **`AuthController`** — unauthenticated and auth flows; register uses `[Transactional]`; responses use **`ApiResponse<T>`** except **HTTP 202** login challenge, which returns **`ApiResponse<TwoFactorChallengeDto>`** in an `Accepted` body.
  - Includes `POST api/auth/forgot-password` and `POST api/auth/reset-password` (token via cache + email abstraction), and **`POST api/auth/verify-email`** (`[AllowAnonymous]`, body `tokenId` + `token` from the email link).
- **`AdminSettingsController`** — runtime app toggles (`GET/PUT api/admin/settings/allow-public-register`).

---

## 7. Caching

- **`ICacheService`** — distributed Redis when `Redis:ConnectionString` is set; otherwise in-memory distributed cache (development).
- Read repositories (e.g. user read) use **`CacheOptions`** and helpers on **`CachedReadRepositoryBase`**; write path can invalidate via **`IUserWriteRepository.InvalidateUserCacheAsync`** where implemented.

---

## 8. Media

- **Images:** `IImageProcessingService` — ImageSharp-based resize.
- **Video:** `IVideoProcessingService` — `VideoProcessing` section in appsettings:
  - Set **`FfmpegPath`** and **`FfprobePath`** to enable **`FfmpegCliVideoProcessingService`**.
  - Leave empty to use **`NullVideoProcessingService`** (probe returns empty; transcode/thumbnail throw until configured).

---

## 9. Messaging (RabbitMQ / Kafka later)

- **`IMessagePublisher`** and **`IMessageConsumer`** — application-facing ports.
- Default implementations: **`NullMessagePublisher`** and **`NullMessageConsumer`**. Swap bindings in DI when you add a real broker adapter.

---

## 10. Worker (cron + queue)

- **`ICronJob`** — name, cron expression (standard 5-field), `ExecuteAsync`.
- **`AddCronScheduling()`** registers `CronJobSchedulerHostedService` (Cronos). Used from **Worker** `Program.cs`.
- Register each job as **`services.AddSingleton<ICronJob, YourJob>()`** (multiple registrations are collected).
- Queue pattern: inherit **`QueueConsumerBackgroundService<TMessage>`**, set `SubscriptionName`, and implement `HandleAsync`.

---

## 11. Configuration checklist (`appsettings`)

| Section | Purpose |
|---------|---------|
| `Database:ApplyPendingMigrations` | When `true`, Api runs **`Database.MigrateAsync()`** at startup (in addition to **Development**, which always migrates). Default `false` in base `appsettings.json`. |
| `Seed:Admin:*` | Bootstrap admin: **`Enabled`**, **`Email`**, **`UserName`**, **`Password`**, **`FirstName`**, **`LastName`**. Seeding runs in **Development** by default, or when **`Seed:Admin:Enabled`** is `true`. Idempotent (skips if email or username already exists). |
| `ConnectionStrings:DatabaseConnectionString` | Write PostgreSQL. |
| `ConnectionStrings:DatabaseReadConnectionString` | Read PostgreSQL (optional). |
| `JwtSettings` | Signing, issuer, audience, token lifetimes. |
| `Redis:ConnectionString` | Optional Redis for cache. |
| `Authentication:Google:ClientId` | Google ID token validation. |
| `ObjectStorage` | S3-compatible storage (e.g. MinIO). |
| `VideoProcessing` | `FfmpegPath`, `FfprobePath`. |
| `Worker` | Cron expressions and worker-host options. |
| `Realtime` | Enable SignalR hub and optional Redis backplane. |

---

## 12. Adding a new feature (short checklist)

1. **Domain** — entity/value object changes; optional domain events (`AddDomainEvent`).
2. **Application** — command/query + handler; validator if needed; use **read repo** for queries and **write / `ForUpdate`** for mutations.
3. **Infrastructure** — only if new persistence, integrations, or interceptors are required.
4. **Api** — partial controller + **`Api/Models`** request types; **`[Authorize]`** / **`[Authorize(Policy = …)]`** as needed; optional **`[JwtAuthorize]`** to pin JWT when multiple schemes exist; **`[Transactional]`** on commands that must be atomic with other writes in the same request.
5. **Events** — choose **`ITransactionalDomainEvent`** vs **`IAfterCommitDomainEvent`** based on whether work must see committed data or outbox semantics.

---

## 13. Quick reference (URLs in this template)

| Method | Route | Notes |
|--------|--------|--------|
| GET | `api/users/profile` | Current user profile (`ApiResponse<UserProfileDto>`). |
| PUT | `api/users/profile` | Update profile (transactional). |
| GET | `api/admin/users` | Admin paged list: `page`, `pageSize`, optional `emailContains`, `userNameContains`, `isLocked`, `role`, `sortBy`, `sortDescending`. |
| POST | `api/admin/users` | Admin create user. |
| PUT | `api/admin/users/{userId}` | Admin edit user profile/role. |
| DELETE | `api/admin/users/{userId}` | Admin soft-delete user. |
| POST | `api/admin/users/{userId}/lock` | Admin lock (body: `LockUserRequest`). |
| POST | `api/admin/users/{userId}/unlock` | Admin unlock user. |
| POST | `api/admin/users/{userId}/impersonate` | Admin issue token pair for target user. |
| POST | `api/admin/users/{userId}/2fa/reset` | Admin reset target 2FA. |
| POST | `api/admin/users/{userId}/password/reset` | Admin reset target password (also revokes refresh tokens). |
| POST | `api/admin/users/{userId}/email/verify` | Admin marks email verified. |
| POST | `api/admin/users/{userId}/email/resend-verification` | Admin sends email verification link (cache + mail). |
| POST | `api/admin/users/{userId}/sessions/revoke` | Revoke all refresh tokens for target user. |
| GET | `api/admin/metrics/dashboard` | Read-only totals + daily registrations (14 days). |
| GET | `api/admin/audit-logs` | Paged audit log; optional `targetUserId`. |
| GET | `api/admin/settings/allow-public-register` | Read register toggle. |
| PUT | `api/admin/settings/allow-public-register` | Set register toggle. |
| POST | `api/auth/forgot-password` | Send password reset token via email abstraction. |
| POST | `api/auth/reset-password` | Reset password using token id + token. |
| POST | `api/auth/verify-email` | Confirm email using token id + token (`[AllowAnonymous]`). |
| POST | `api/samples/transactions/manual-lock/{userId}` | Manual UoW example (admin). |

For behaviour and configuration details (audit writer, migration backfill, verification email), see **`DEVELOPER_GUIDE.md`** §9.

Swagger is enabled in **Development** in `Api/Program.cs`.

---

## 14. EF migrations, local stack, Docker, and startup behaviour

- **Pending migrations:** In **Development**, the Api host always applies **`MigrateAsync()`** on startup. In other environments, set **`Database:ApplyPendingMigrations`** to `true` if you want the same behaviour (typical for a single-node deploy; for multi-instance production, prefer running migrations as an explicit release job).
- **Admin seed:** In **Development**, the Api seeds a default admin (see **`Seed:Admin`** in `appsettings.Development.json`) unless a user with that email or username already exists. For staging/production, set **`Seed:Admin:Enabled`** to `true` and supply a strong **`Password`** via secrets or environment variables—never commit production credentials.
- Run migrations against the **write** database (same schema should be applied to a physical read replica when used).
- **From repo root:** `./tools/backend/migrate.sh` (see `tools/README.md`). This script lives outside `src/Backend/` source.
- **Easy local dependencies (Postgres read/write URLs, Redis, MinIO, OpenSearch):** use repo root **`local/`** — `docker compose -f local/docker-compose.yml up -d` and copy `local/.env.example` into `src/Backend/Api` or merge values into `appsettings.Development.json`.
- **Production / remote images:** see **`deploy/README.md`** (add a `Dockerfile` and compose/K8s manifests when you wire CI).

If you extend this guide, keep it aligned with actual types and routes in the repository so newcomers can search the codebase by the names listed here.
