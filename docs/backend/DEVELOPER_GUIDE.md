# Backend developer guide

Practical onboarding for this solution: what is already wired, how authentication and authorization differ, how to map DTOs, how background work runs, how logging behaves across hosts, how to extend workers and tests, and **admin audit / email verification / metrics** (see **§9**). For architecture and layer rules, see **`BASE_GUIDE.md`** and **`ARCHITECTURE.md`**.

---

## 1. Is the baseline “enough” for a real project?

The solution already includes patterns many production APIs use:

- **Security:** JWT bearer auth, refresh tokens, optional Google sign-in, 2FA hooks, rate limiting on auth endpoints, security headers, forwarded headers for reverse proxies.
- **Data:** EF Core with PostgreSQL, optional read connection, migrations, seeding, auditable entities, domain events (including after-commit queue), transactional filter for HTTP actions.
- **Optimistic concurrency:** `rowVersion` on every `Entity<TId>`; mutating APIs require the client to send the version from the last GET (see **`OPTIMISTIC_CONCURRENCY.md`**).

**Startup (Api):** pending EF migrations are applied when the host is **Development** or when **`Database:ApplyPendingMigrations`** is `true`. A **bootstrap admin** user is seeded when **Development** or **`Seed:Admin:Enabled`** is `true` (see **`Seed:Admin`** in `appsettings`; idempotent on email/username).

- **Cross-cutting:** MediatR-style commands/queries, FluentValidation, global exception handling, health checks, correlation ID middleware, Serilog with a shared bootstrap (`Observability`).
- **Integrations (pluggable):** Redis-backed or in-memory cache, S3-compatible object storage, null email/messaging implementations you can swap for real providers, optional SignalR + Redis backplane, image/video processing abstractions.

Common additions teams introduce **when requirements appear** (not mandatory on day one):

- **Distributed tracing / metrics:** OpenTelemetry + Prometheus or vendor APM.
- **API versioning** and stricter OpenAPI contracts for public APIs.
- **Outbox / message bus** for reliable integration events (the repo has `IMessagePublisher` / `IMessageConsumer` stubs).
- **Dedicated job runner** (Quartz.NET, Hangfire with storage) if you need clustered schedules, misfire policies, or dashboards—the in-process cron host is intentionally lightweight.
- **Feature flags** beyond `AppSettings` (e.g. LaunchDarkly) if product toggles are complex.

Treat the current stack as a solid default; add the above when scale or compliance demands it.

---

## 2. Authentication vs authorization (JWT and policies)

- **Authentication (“who are you?”)** is handled by **JWT Bearer** (`Infrastructure` `AddAuth`). The API validates the access token and builds `HttpContext.User`.
- **Authorization (“what may you do?”)** is ASP.NET Core **policies** and attributes.

Examples in this repo:

- **`[Authorize]`** — any authenticated user (see `AuthController` 2FA endpoints).
- **`[Authorize(Policy = AuthorizationPolicies.AdminOnly)]`** — users whose JWT includes the **Admin** role claim (`Api/DependencyInjection.cs` maps the policy with `RequireRole`).

To support **multiple roles** on one endpoint, extend policies in `AddAuthorization`:

```csharp
options.AddPolicy("AdminOrSupport", policy =>
    policy.RequireRole(UserRoles.Admin, "Support"));
```

Or use **`IAuthorizationHandler`** / requirements for permission-based rules (e.g. “can edit tenant X”) instead of roles only.

JWT creation puts `ClaimTypes.Role` from the domain user (`JwtGenerator`); the bearer middleware is configured with `RoleClaimType = ClaimTypes.Role` so `RequireRole` matches.

### `[JwtAuthorize]` (`Api/Authorization/JwtAuthorizeAttribute.cs`)

This attribute only sets **`AuthenticationSchemes`** to **JWT Bearer**. It does **not** replace `[Authorize]` or policies: it tells the host which authentication handler to use when challenging the client.

- **You do not have to use it on every endpoint.** JWT is already the **default** scheme in `AddAuth`, so **`[Authorize]`** / **`[Authorize(Policy = …)]`** alone is usually enough.
- **Use `[JwtAuthorize]`** when you want to be explicit in code, or when you later add **another scheme** (cookies, API keys, etc.) and need this controller or action to **accept only JWT** for authentication.

Some admin controllers in this repo combine **`[JwtAuthorize]`** + **`[Authorize(Policy = …)]`** for clarity and future-proofing; dropping `[JwtAuthorize]` would still work while JWT remains the sole scheme.

---

## 3. DTO mapping (Mapperly + manual)

The solution uses **Mapperly** (compile-time source generator) for repetitive **domain/entity → application DTO** maps, and **manual** mapping only where shapes differ (registration messages, 2FA challenge, computed metrics).

### 3.1 Where mappers live

| Layer | Folder | Reuse |
|-------|--------|--------|
| **Application** | `Application/Mapping/` | `UserMapper`, `AdminAuditLogMapper` — call from handlers, Infrastructure, or anywhere that references `Application`. |
| **Api** | `Api/Mapping/` | HTTP-only contracts (e.g. `AuthApiMapper` → `AuthResponseDto`). |

**Shared conversions** (e.g. `UserId` → `Guid`) are defined once in `MappingConversions` and pulled in via `[UseStaticMapper(typeof(MappingConversions))]` — no per-mapper duplicate config.

**Assembly defaults** (`MappingAssemblyDefaults.cs`): `RequiredMappingStrategy.Target` so new DTO properties fail the build until mapped.

### 3.2 How to add a new map

1. Add a method to an existing `[Mapper]` class in `Application/Mapping/`, or create `XxxMapper.cs` with `[Mapper]` + `[UseStaticMapper(typeof(MappingConversions))]` when mapping `User` / value objects.
2. `dotnet build` — generated code appears under `obj/`; call `entity.ToXxxDto()` or `entities.ToXxxDtos()`.
3. For **nested or renamed** API fields, add `[MapProperty(...)]` on the Api mapper (see `AuthApiMapper`).

### 3.3 When to stay manual

- One-off shapes, security-sensitive fields, or multi-step composition (dashboard metrics, registration text).
- Prefer `record` constructors in queries when the projection is not a 1:1 entity map.

**AutoMapper** is not used (removed due to NU1903); do not reintroduce without team agreement.

---

## 3a. Unit of Work and SaveChanges ownership

The repository pattern in this solution **only stages** mutations. `SaveChanges` is owned by `IUnitOfWork` (`EfUnitOfWork`) so that:

- Every command flushes **once**, deterministically, before returning.
- Handlers that touch multiple ports (e.g. `IUserRespository` + `IAdminAuditLogWriter`) share the same transaction without coupling repos to each other.
- Tests do not need to mock `SaveChangesAsync` on every port.

### Pattern for command handlers

```csharp
public sealed class XxxCommandHandler : IRequestHandler<XxxCommand, Result<bool, Error>>
{
    private readonly IUserRespository _users;
    private readonly IUnitOfWork _uow;

    public XxxCommandHandler(IUserRespository users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<bool, Error>> Handle(XxxCommand request, CancellationToken ct)
    {
        var user = await _users.GetUserByIdForUpdateAsync(request.UserId, ct);
        if (user is null) return new NotFoundError("...");

        user.DoSomething();
        await _uow.SaveChangesAsync(ct);
        return true;
    }
}
```

The `TransactionalActionFilter` wraps each HTTP action with **BeginTransaction -> action -> Commit/Rollback** based on response status, so a successful `IUnitOfWork.SaveChangesAsync` is committed atomically (and rolled back on >= 400 / exceptions).

### Read side cannot SaveChanges

`ApplicationReadDbContext` overrides `SaveChanges`/`SaveChangesAsync` to throw `InvalidOperationException`. Combined with `NoTracking` + `AutoDetectChangesEnabled=false`, accidental writes via the read DbContext **fail fast at runtime** — use `ApplicationDbContext` (write) and the repository ports for mutations.

---

## 3b. Caching: read-through vs ephemeral business state

The codebase uses `ICacheService` (Redis-backed in production, in-memory in development) for two distinct purposes — **placing them in the wrong layer is the most common cache bug**.

### Decision rule

> **Cache an entity read from DB → repository (read-through).  
> Cache a short-lived business artifact that has no DB row → handler (ephemeral state).**

| Concern | Read-through cache | Ephemeral business state |
|---------|--------------------|---------------------------|
| Lives because... | DB read is expensive | Token / challenge / pending workflow needs to be remembered between requests |
| Has a backing DB row? | Yes | **No** — cache **is** the source of truth until TTL |
| TTL semantics | Performance hint | Business rule (e.g. "verification link valid 24h") |
| Invalidation trigger | Mutation on the entity | Token consumed, user cancels, TTL expires |
| Layer that owns it | **Repository** | **Handler / Application** |

### Pattern A — Read-through cache (Repository)

Already implemented in `UserReadRepository`:

- `GetByIdAsync`, `GetByEmailAsync`, `GetByUserNameAsync` check the cache first, fall back to EF, then populate it.
- Write-side helpers (`UserWriteRepository.InvalidateUserCacheAsync` /  `IUserRespository.InvalidateUserCacheAsync`) clear the same keys after the unit-of-work commits.
- Handlers stay oblivious — they just call `_users.GetUserByEmailAsync(email)` and `await _users.InvalidateUserCacheAsync(...)` after mutation.

Adding a new read-through cache:

1. Put the cache key constants in `Infrastructure/.../Repositories/<entity>ReadCacheKeys.cs`.
2. Wrap the EF query in the read repository (cache miss → query → set).
3. In the write repository, call `RemoveCacheKeysAsync(...)` (or a typed `InvalidateXxxCacheAsync` helper) after the handler calls `_uow.SaveChangesAsync(ct)`.
4. **Never** touch these keys from a handler.

### Pattern B — Ephemeral business state (Handler)

Already used by:

- **Email verification token** — `VerifyEmailCommands.cs`, `EmailVerificationSender.cs`.
- **Password reset token** — `ForgotPasswordCommands.cs`.
- **Two-factor pending challenge** — 2FA login flow.

These artifacts never hit the database. The cache key + TTL **is** the lifecycle:

```csharp
string cacheKey = AuthCacheKeys.EmailVerificationPrefix + tokenId;
await _cache.SetAsync(cacheKey, new EmailVerificationTokenPayload(userId, hash), ttl, ct);
// ...later...
EmailVerificationTokenPayload? payload = await _cache.GetAsync<...>(cacheKey, ct);
if (payload is null) return new InvalidOrExpiredTokenError();
await _cache.RemoveAsync(cacheKey, ct);   // explicit "consume"
```

Rules:

- Put the prefix in a static `XxxCacheKeys` class under `Application/Modules/.../`.
- The handler that **creates** the token also defines its TTL.
- The handler that **consumes** the token deletes the key explicitly (don't rely on TTL alone for one-time tokens).
- Do **not** wrap this in a repository — it isn't an entity.

### Anti-patterns

| Smell | Why it hurts |
|------|--------------|
| `_cache.Set("user:" + id, user)` inside a command handler | Read-through belongs to the repo. Other handlers reading the same user won't see the cache; invalidation becomes "whoever remembers". |
| Repo setting TTL based on business rules (e.g. "verification token lives 24h") | Repo is leaking business semantics — violates Clean Architecture. |
| Repo invalidating cache for tokens, pending challenges, or other handler-owned artifacts | Repo should only invalidate caches **it itself populates**. |
| Reading the cache directly in a controller | Controllers shouldn't bypass the handler / repository layer. |

### Where to look in code

- Contract: `Application/Contracts/Caching/ICacheService.cs`
- Read-through example: `Infrastructure/Modules/Users/Persistence/Repositories/UserReadRepository.cs` + `UserReadCacheKeys`
- Ephemeral example: `Application/Modules/Users/Authentication/Commands/VerifyEmail/VerifyEmailCommands.cs` + `AuthCacheKeys`
- Invalidation entry points (write side): `IUserRepository.InvalidateUserCacheAsync(...)` called from every command handler after `_uow.SaveChangesAsync(ct)`.

---

## 3c. Transactional outbox for integration events

In-process domain events (`ITransactionalDomainEvent`, `IAfterCommitDomainEvent`) cover same-aggregate consistency and post-commit MediatR handlers. **Anything that has to leave this process** (RabbitMQ publish, webhook, cross-bounded-context message) goes through the **transactional outbox** to keep producer-side state and broker delivery atomic.

### Producing an integration event

1. Define a record implementing `IIntegrationEvent`:

   ```csharp
   public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email)
       : IIntegrationEvent
   {
       public Guid EventId { get; } = Guid.NewGuid();
       public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
       public string RoutingKey => "user.registered.v1";
   }
   ```

2. Add it from the aggregate the same way as any other domain event: `AddDomainEvent(new UserRegisteredIntegrationEvent(...))`.

3. The `DomainEventsSaveChangesInterceptor` runs during `SavingChangesAsync` and serialises the event into an `OutboxMessages` row inside the same EF `DbContext`. Because `IUnitOfWork.SaveChangesAsync` is the single flush point, the business row and the outbox row commit (or roll back) together.

### Draining the outbox

`OutboxPublisherHostedService` (registered via `AddOutboxPublisher` in the Worker host) polls `OutboxMessages WHERE ProcessedOnUtc IS NULL AND (NextAttemptUtc IS NULL OR NextAttemptUtc <= now())`, publishes the JSON payload via `IMessagePublisher.PublishRawAsync` to RabbitMQ, and marks the row processed. Failures use exponential backoff (`InitialBackoffSeconds × 2^attempts`, capped at 1 h); rows that exceed `MaxAttempts` are marked abandoned with the error captured.

### Configuration (`appsettings.json` → `Outbox` section)

| Key | Default | Notes |
| --- | --- | --- |
| `Enabled` | `true` | Disable for API-only deployments without a worker. |
| `PollIntervalSeconds` | `5` | Idle delay between cycles when no rows are pending. |
| `BatchSize` | `50` | Rows pulled per cycle. |
| `MaxAttempts` | `10` | After this many failures the row is abandoned. |
| `InitialBackoffSeconds` | `10` | Exponential backoff seed. |

### Important rules

- **Only the Worker host drains the outbox** — registering `AddOutboxPublisher` in the API would cause double publishes.
- **Consumers must be idempotent on `EventId`**: the outbox guarantees at-least-once delivery, not exactly-once.
- Domain events should be **either** `IIntegrationEvent` (cross-process) **or** an in-process marker, not both. If you need to react in-process AND publish, raise two events.

---

## 4. Scheduling and the Worker host

### 4.1 Where cron runs

`Infrastructure.Scheduling.CronJobSchedulerHostedService` resolves all **`ICronJob`** implementations from DI and runs them on a UTC cron schedule (Cronos).

- The **Worker** project calls **`AddCronScheduling()`** and registers **`SampleCronJob`** — this is the supported place for recurring jobs.
- The **Api** project does **not** register `AddCronScheduling()` by default. You *could* register cron jobs inside the web process for local experiments, but **production** recurring work should usually run in **Worker** (or an external scheduler) so web instances stay stateless and horizontally scalable.

### 4.2 Three worker “shapes” (triggers)

| Pattern | Type | Purpose |
|---------|------|--------|
| **Cron** | `ICronJob` + `AddCronScheduling()` | Time-based schedules (UTC, five-field cron). Example: `SampleCronJob`. |
| **Queue** | `QueueConsumerBackgroundService<TMessage>` | Pull/consume messages from `IMessageConsumer`. Example: `SampleQueueConsumerService` + `SampleUserEvent`. |
| **Long-running loop** | `BackgroundService` | Polls, periodic housekeeping, or bridge until a real broker exists. Example: `SampleBackgroundService` (opt-in via `Worker:EnableSampleBackgroundLoop` in configuration). |

Register new cron jobs as **`services.AddSingleton<ICronJob, YourJob>()`** (multiple registrations are supported). Tune cron strings under the **`Worker`** configuration section (`WorkerSchedulingOptions`).

**Quartz (default in Worker):** `AddWorkerScheduling()` registers **Quartz.NET** with a hosted registrar. Set **`Worker:UseQuartzScheduler`** to `false` to fall back to the lightweight **Cronos** in-process host. For clustered schedules, set **`Worker:Quartz:UsePersistentStore`** to `true` (uses the same PostgreSQL connection string as the app).

**SMTP email:** enable **`Smtp:Enabled`** and set host/port/credentials. Local dev: `docker compose -f local/docker-compose.yml up -d mailhog` and use port **1025** without SSL (`UseSsl: false`). UI: http://localhost:8025

**RabbitMQ:** enable **`Messaging:RabbitMq:Enabled`**. Local: `docker compose ... up -d rabbitmq` (management UI port **15672**).

---

## 5. Tests

- **`Tests/Application.Tests`** — unit tests for validators and handlers (e.g. login rejects unverified email).
- **`Tests/Api.IntegrationTests`** — HTTP tests with **Testcontainers** PostgreSQL (skipped if Docker is unavailable). Override with env **`TEST_DATABASE_CONNECTION_STRING`**.

Run tests from the backend solution directory:

```bash
dotnet test src/Backend/Backend.sln
```

---

## 6. Observability and log indices

`PlatformSerilog.Configure("api")` and `RegisterForHostApplicationBuilder(..., "worker")` add a **`Service`** property to every log event (`"api"` vs `"worker"`).

- **Console-only** configs (default `appsettings`) do not create Elasticsearch indices.
- When you add **Elasticsearch / OpenSearch / Seq / Datadog**, **index or stream names are not auto-created per project file**—you configure sinks in `appsettings` or environment variables. Typical patterns:
  - **Separate indices:** set `indexFormat` (or equivalent) per host, for example `logs-api-{0:yyyy.MM.dd}` vs `logs-worker-{0:yyyy.MM.dd}`, *or* include the service name in the template if your sink supports it.
  - **Shared index:** one index such as `logs-{0:yyyy.MM.dd}` and **filter in your log UI** on `Service = "worker"` (and `Environment`).

Worker `appsettings.Development.json` may use OpenSearch with a shared `indexFormat`; rely on the **`Service`** field to tell Api and Worker logs apart unless you change the template per deployment.

---

## 7. Adding a new worker

There is no single “WorkerBase” class for every case; use the pattern that matches the trigger:

1. **New hosted service in the same Worker project** (fastest): implement `ICronJob`, `BackgroundService`, or `QueueConsumerBackgroundService<T>`, then register it in **`Worker/Program.cs`**.
2. **New executable** (e.g. `Worker.Notifications`): copy **`Worker.csproj`** structure—reference **`Application`**, **`Infrastructure`**, **`Observability`**, call `AddApplication()`, `AddInfrastructure()`, Serilog bootstrap with a **new service name** (e.g. `"notifications-worker"`), then register only the jobs/consumers that host needs.

Keep **shared business logic** in **Application** handlers/services; keep **host wiring** in **Worker** `Program.cs`.

---

## 8. Quick checklist for a new feature

1. Domain change in **Domains** (if needed).
2. Command/query + handler + validator in **Application**.
3. Persistence in **Infrastructure** (config, repository).
4. HTTP contract in **Api** (`Models`, controller); apply **`[Authorize]`** / **policy** as needed.
5. If background work: **Worker** + tests.

---

## 9. Admin platform: audit trail, email verification, sessions, and metrics

This section describes behaviour added for **admin operations**, **security review**, and **lightweight product metrics**. All admin HTTP routes require the **`AdminOnly`** policy unless noted.

### 9.1 Domain fields (`Domains/UserAggregate/User`)

- **`RegisteredAtUtc`** — set when a user self-registers (`RegisterCommandHandler`) or when an account is created via **Google** (new user) or **admin create** (trusted onboarding). Used by **`AdminDashboardMetricsQuery`** for “signups per day”.
- **`EmailVerifiedAtUtc`** — set when email is confirmed (admin action or public verify flow). **Self-service register** does not set it until the user completes verification (or an admin marks it). **Admin-created users** are marked registered and verified immediately (operator-trusted onboarding).

### 9.2 Audit log

- **Table:** `AdminAuditLogs` (entity `Domains/Administration/AdminAuditLog`).
- **Writer:** `IAdminAuditLogWriter` (`Infrastructure/Administration/AdminAuditLogWriter.cs`) appends rows on the **same `ApplicationDbContext`** as user writes; they persist on the next **`SaveChanges`** from `IUserRespository`.
- **Action keys:** `Application/Modules/Users/Administration/AdminAuditActions.cs` (e.g. `user.impersonated`, `user.password_reset`, `user.sessions_revoked`).
- **HTTP:** `GET api/admin/audit-logs` — paging (`page`, `pageSize`), optional **`targetUserId`** filter. Response type: `AdminAuditLogListDto`.

Sensitive admin actions pass **`ActorIpAddress`** from the API (`ClientIp` on `AdminUsersController`) into commands where implemented.

### 9.3 Email verification

| Step | Endpoint / handler |
|------|---------------------|
| Admin marks verified immediately | `POST api/admin/users/{userId}/email/verify` → `AdminMarkEmailVerifiedCommand` |
| Admin sends link | `POST api/admin/users/{userId}/email/resend-verification` → `AdminResendEmailVerificationCommand` stores a short-lived payload in **`ICacheService`** (key prefix `auth:email-verify:`) and emails instructions via **`IEmailSender`**. Link format matches **`PasswordReset:PublicAppBaseUrl`** (same options section as forgot-password TTL). |
| User completes verification | `POST api/auth/verify-email` (`[AllowAnonymous]`) body **`VerifyEmailRequest`** (`tokenId`, `token`) → `VerifyEmailCommand` |

If **`PublicAppBaseUrl`** is empty, the email contains raw token values for dev (same pattern as password reset).

### 9.4 Session revocation (refresh tokens)

- **`IUserRespository.RevokeAllRefreshTokensForUserAsync`** — sets **`RevokedAtUtc`** on all non-revoked refresh tokens for that user (bulk `ExecuteUpdate`).
- **`POST api/admin/users/{userId}/sessions/revoke`** — explicit “log out everywhere” for the target user.
- **`AdminResetPasswordCommand`** — after a successful password change, **all refresh tokens for that user are revoked** automatically.

Access (JWT) tokens are **not** revoked server-side (short-lived by design); only refresh rotation is invalidated.

### 9.5 Metrics

- **`GET api/admin/metrics/dashboard`** — `AdminDashboardMetricsQuery`: total **non-deleted** users plus **daily registration counts for the last 14 days** (filled with zeros for days without signups). Depends on **`RegisteredAtUtc`** being populated for new registrations.

### 9.6 User list filters

- **`GET api/admin/users`** — optional query parameters: **`emailContains`**, **`userNameContains`**, **`isLocked`**, **`role`**, **`sortBy`** (`username` \| `email` \| `registeredat` \| `role`), **`sortDescending`**. Soft-deleted users are excluded from the admin list query.

### 9.7 Migration note (`AdminAuditEmailMetrics`)

The EF migration adds columns and the audit table, then runs SQL to **backfill** `RegisteredAtUtc` and `EmailVerifiedAtUtc` for rows that were still `NULL` (legacy databases). Adjust or remove that SQL in production if your policy is stricter (e.g. never auto-verify legacy users).

---

## 10. Related deep-dives (implemented patterns)

| Topic | Doc |
|-------|-----|
| Api ↔ Worker, outbox, RabbitMQ, no JWT on Worker | [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) |
| DataProtection key ring (2FA, deploy) | [`DATA_PROTECTION.md`](./DATA_PROTECTION.md) |
| EF LINQ pitfalls (`Id.Value`, `DateOnly` GroupBy, retry strategy) | [`EF_PITFALLS.md`](./EF_PITFALLS.md) |

**Already in this solution:** transactional outbox, Quartz cron in Worker, OpenTelemetry registration in `Observability`. Optional later: contract tests (Schemathesis), dedicated notification worker executable.

This guide is the living “how we work” companion to **`BASE_GUIDE.md`**.
