# Backend Codebase Included and Usage Guide

This file is the practical "what is inside this base and how to use it" document for onboarding developers and future projects.

Use with:

- `BASE_GUIDE.md` for detailed flow examples.
- `ARCHITECTURE.md` for DDD boundaries.
- `LLM_FEATURE_IMPLEMENTATION_RULES.md` for strict feature implementation rules.

---

## 1) What is included in this backend base

Source root: `src/Backend/`

### Projects

| Project | Purpose |
|---|---|
| `Api` | HTTP entrypoint, auth/authorization, controllers, response envelope, middleware, health, realtime endpoint mapping. |
| `Application` | Use cases (commands/queries), handlers, contracts/ports, validators, app orchestration. |
| `Domains` | Entities/aggregates/value objects/domain events. Business rules must live here or Application handlers. |
| `Infrastructure` | EF Core contexts/migrations/repositories, cache, JWT implementation, storage/media/messaging adapters. |
| `Worker` | Background host for cron and queue-driven workers. |
| `Observability` | Shared Serilog bootstrap for API and Worker hosts. |
| `Shared` | Common result/error primitives used by app layers. |

### Core capabilities already wired

- JWT auth + role policy baseline. Unauthorized/forbidden responses use the shared `ApiResponse<T>` envelope (see `JwtEnvelopeEvents`).
- Refresh-token session flow (`/auth/refresh`) with hashed refresh token storage **plus theft detection**: a revoked-token replay revokes every active session for the user and logs a security event.
- Case-insensitive email/username lookups via `NormalizedEmail` / `NormalizedUserName` (ASP.NET Identity-style upper-invariant normalization at the aggregate boundary).
- Read/write DB context split (`ApplicationDbContext` + `ApplicationReadDbContext` exposed as `IApplicationReadDbContext` under `Application.Contracts.Persistence`).
- Repository base helpers for cache/read/write reuse.
- Admin user management endpoints (create/edit/delete/lock/unlock/impersonate/reset 2FA/reset password).
- Runtime app settings table + admin toggle API (example: public registration on/off).
- Forgot/reset password flow scaffold (`/auth/forgot-password`, `/auth/reset-password`) via email abstraction.
- Domain event pipeline (transactional + after-commit queue).
- Transactional outbox with **publisher confirms** (`WaitForConfirmsOrDie`) and `AbandonedAtUtc` for poison-row separation; an `OutboxCleanupJob` keeps the table bounded.
- RabbitMQ consumer hardening: `DispatchConsumersAsync=true`, per-channel `BasicQos(prefetchCount)`, dead-letter queues, **admin tooling** (`AdminMessagingController`: peek/replay/drain).
- **Correlation IDs** propagated end-to-end: HTTP middleware → `ICorrelationContext` (AsyncLocal) → `OutboxMessage.CorrelationId` → AMQP `BasicProperties.CorrelationId` → worker logger scope.
- Transaction patterns (`[Transactional]` filter and `IUnitOfWork`).
- Distributed cache abstraction (`ICacheService`) with Redis fallback support.
- Optional realtime hub (`Realtime:Enabled`) with optional Redis SignalR backplane.
- Media pipeline (presigned PUT → ClamAV scan → ffmpeg/ImageSharp variants → HLS) with stale-recovery cron, storage cleanup cron, magic-byte verification, and **streaming temp-file outputs** (no >100 MB in managed memory per rung).
- Background execution scaffolding:
  - Cron via `ICronJob` + `CronJobSchedulerHostedService` (or `AddWorkerScheduling` Quartz path)
  - Queue worker base via `QueueConsumerBackgroundService<TMessage>`
- API security: CORS (`Cors.AllowedOrigins`), rate limiting (`auth`, `media-upload`), CSP + X-Frame-Options + Referrer-Policy + Permissions-Policy.
- Structured logging (Serilog), OpenTelemetry traces/metrics, health checks (`/health`).

---

## 2) Local run flow (recommended)

1. Start infra dependencies from repo root:
   - `docker compose -f local/docker-compose.yml up -d`
2. Configure API settings:
   - `src/Backend/Api/appsettings.Development.json` (or secrets/env vars)
3. Apply migrations:
   - `./tools/backend/migrate.sh`
4. Run hosts:
   - API: `dotnet run --project src/Backend/Api`
   - Worker: `dotnet run --project src/Backend/Worker`
   - Frontend (if needed): run from `src/Frontend`

---

## 3) How to add a new feature safely

1. Add/extend domain model under `Domains/Modules/<Feature>/...`.
2. Add Application contracts + handlers under:
   - `Application/Contracts/Modules/<Feature>/...`
   - `Application/Modules/<Feature>/...`
3. Add infrastructure implementation under:
   - `Infrastructure/Modules/<Feature>/...`
4. Expose HTTP endpoints in `Api/Controllers/<Feature>/...` and request/response models in `Api/Models/...`.
5. Keep controller thin: map HTTP -> MediatR/use-case call -> map result to `ApiResponse<T>`.
6. If mutation touches persistence, use transactional path and write-side repository methods.
7. If read performance matters, add cache key strategy and invalidate after writes.

---

## 4) Worker model in this base

The base supports 3 execution styles:

1. **Cron worker**
   - Register jobs as `ICronJob` and call `AddCronScheduling()`.
2. **Queue worker**
   - Inherit `QueueConsumerBackgroundService<TMessage>`.
   - Implement `SubscriptionName` + `HandleAsync`.
   - Start with `NullMessageConsumer`; replace with Rabbit/Kafka adapter later.
3. **Polyglot worker (other languages)**
   - Keep message schema/contracts stable and broker-compatible.
   - Treat .NET worker as one consumer implementation, not the only one.

---

## 5) Production-readiness checklist (minimum)

- Set strict `JwtSettings` and production secrets management.
- Configure trusted proxy/network settings correctly.
- Add CORS policy for frontend origins.
- Configure OpenSearch/ELK security (no dev insecure flags).
- Add integration tests for critical API paths.
- Add load tests for target traffic and tune DB indexes/caching.
- Add queue reliability patterns if using broker:
  - retries
  - dead-letter handling
  - idempotency
  - outbox/inbox semantics as needed

---

## 6) What this base intentionally does not force

- No lock-in to one queue broker vendor.
- No forced microservice split (modular monolith first).
- No mandatory heavy scheduler (Quartz/Hangfire can be added later).
- No mandatory OpenTelemetry pipeline yet (can be layered in).

This keeps the base flexible for different customer projects while preserving consistent coding patterns.
