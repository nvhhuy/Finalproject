# Backend developer guide

Practical onboarding for this solution: what is already wired, how authentication and authorization differ, how to map DTOs, how background work runs, how logging behaves across hosts, how to extend workers and tests, and **admin audit / email verification / metrics** (see **§9**). For architecture and layer rules, see **`BASE_GUIDE.md`** and **`ARCHITECTURE.md`**.

---

## 1. Is the baseline “enough” for a real project?

The solution already includes patterns many production APIs use:

- **Security:** JWT bearer auth, refresh tokens, optional Google sign-in, 2FA hooks, rate limiting on auth endpoints, security headers, forwarded headers for reverse proxies.
- **Data:** EF Core with PostgreSQL, optional read connection, migrations, seeding, auditable entities, domain events (including after-commit queue), transactional filter for HTTP actions.

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

## 3. DTO mapping: manual vs AutoMapper

The solution uses **both** patterns:

- **Manual** at the HTTP edge and for auth payloads (e.g. `AuthResponseDto.From`, controller helpers).
- **AutoMapper** for a small set of application DTOs (`Application/Mappings/UserMappingProfile.cs`, e.g. `User` → `UserSummaryDto`), registered in `Application` `DependencyInjection` via `AddAutoMapper`.

| Approach | Pros | Cons |
|----------|------|------|
| **Manual** | Compile-time safety, easy to read in code review, no extra convention magic, fits DDD boundaries. | More lines for large CRUD surfaces. |
| **AutoMapper** | Less boilerplate for repetitive property-to-property maps. | Runtime configuration errors, harder refactors, “hidden” mapping logic. |

**Recommendation:** keep **manual** mapping for auth, security-sensitive, and unstable shapes. Use **AutoMapper** (or **Mapperly**) for repetitive, stable entity-to-read-model maps if the team prefers profiles over constructor mapping. Note: the referenced AutoMapper package version should be upgraded when you next touch dependencies (security advisories).

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

---

## 5. Tests

- **`Tests/Application.Tests`** — unit tests for **FluentValidation** validators and other pure application rules without a database. Examples: admin user validators, register/login validators.
- **Next steps for “full” coverage:** add **handler tests** with mocked `IUserRespository` / `IUserReadRepository`, and **integration tests** using `WebApplicationFactory` for HTTP + auth + DB (slower, but catch wiring bugs).

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

## 10. Optional improvements worth researching later

- **OpenTelemetry** tracing from Api and Worker with W3C trace context propagation into logs.
- **Outbox pattern** for reliable domain event publication.
- **Quartz.NET or Hangfire** if cron must survive restarts and run once per cluster.
- **Contract tests** for public APIs (e.g. Schemathesis against OpenAPI).

This guide is the living “how we work” companion to **`BASE_GUIDE.md`**.
