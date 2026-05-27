# Backend feature catalog

List of **features already included in the base** — usable immediately after local/prod configuration. This is the official "menu"; update this file when adding new features.

**API version:** `v1.0`  
**Base path:** `api/v1/...` (e.g. `POST /api/v1/auth/login`)

---

## How to read the tables

| Column | Meaning |
|--------|---------|
| **Ready** | Code + endpoint/job exist; only config / dependency needed |
| **Needs config** | Must set connection string, secret, or `Enabled: true` |
| **Dev only** | Should only be enabled in Development |

---

## 1. Authentication & session

| Feature | Status | Endpoint / flow |
|---------|--------|-----------------|
| Register (local) | Ready | `POST /api/v1/auth/register` |
| Resend verification email | Ready | `POST /api/v1/auth/resend-verification` |
| Verify email (token) | Ready | `POST /api/v1/auth/verify-email` |
| Email/password login | Ready | `POST /api/v1/auth/login` |
| Refresh token (rotation) | Ready | `POST /api/v1/auth/refresh` |
| Refresh token reuse detection | Ready | Automatic in refresh handler; revokes all sessions + audit |
| Google sign-in | Needs config | `POST /api/v1/auth/google` — `Authentication:Google:ClientId` |
| Forgot password | Needs SMTP | `POST /api/v1/auth/forgot-password` |
| Reset password (token) | Ready | `POST /api/v1/auth/reset-password` |
| 2FA — begin setup | Ready (logged in) | `POST /api/v1/auth/2fa/begin` |
| 2FA — confirm enable | Ready | `POST /api/v1/auth/2fa/confirm` |
| 2FA — complete login | Ready | `POST /api/v1/auth/login/2fa` |
| Block public registration | Ready | App setting `allow_public_register` (admin API) |
| Failed login lockout | Ready | Cache-based tracker; no separate endpoint |
| JWT access token | Ready | Claims: `sub`, `role`, `two_factor` (PII trimmed) |
| Auth rate limit | Ready | `auth` policy on `AuthController` |

**Related security:** PBKDF2 600k iterations, password complexity policy, anti-enumeration login, 2FA brute-force + replay protection.

---

## 2. User (self-service)

| Feature | Status | Endpoint |
|---------|--------|----------|
| View profile | Ready (JWT) | `GET /api/v1/users/profile` |
| Edit profile | Ready | `PUT /api/v1/users/profile` |

---

## 3. Admin — user management

**Requires:** JWT role `Admin` (`AuthorizationPolicies.AdminOnly`).

| Feature | Endpoint |
|---------|----------|
| List users | `GET /api/v1/admin/users` |
| Create user | `POST /api/v1/admin/users` |
| Update user (name, email, role, …) | `PUT /api/v1/admin/users/{userId}` |
| Soft delete user | `DELETE /api/v1/admin/users/{userId}` |
| Lock account | `POST /api/v1/admin/users/{userId}/lock` |
| Unlock account | `POST /api/v1/admin/users/{userId}/unlock` |
| Impersonate (issue admin-as-user token) | `POST /api/v1/admin/users/{userId}/impersonate` |
| Reset 2FA | `POST /api/v1/admin/users/{userId}/2fa/reset` |
| Reset password (admin) | `POST /api/v1/admin/users/{userId}/password/reset` |
| Mark email verified | `POST /api/v1/admin/users/{userId}/email/verify` |
| Resend verification email | `POST /api/v1/admin/users/{userId}/email/resend-verification` |
| Revoke all sessions | `POST /api/v1/admin/users/{userId}/sessions/revoke` |

All write operations are audited via `IAdminAuditLogWriter` (same transaction as user write).

---

## 4. Admin — settings, audit, metrics

| Feature | Endpoint |
|---------|----------|
| Read public registration toggle | `GET /api/v1/admin/settings/allow-public-register` |
| Enable/disable public registration | `PUT /api/v1/admin/settings/allow-public-register` |
| View audit log (paginated) | `GET /api/v1/admin/audit-logs` |
| Dashboard metrics (user counts, …) | `GET /api/v1/admin/metrics/dashboard` |

**App settings:** `AppSettings` table + `IAppSettingsService` — extend with new keys using the existing pattern.

---

## 5. Admin — messaging (DLQ)

**Requires:** RabbitMQ enabled + queue configured with DLQ.

| Feature | Endpoint |
|---------|----------|
| Peek dead-letter messages | `GET /api/v1/admin/messaging/dlq/{queueName}/peek` |
| Replay DLQ | `POST /api/v1/admin/messaging/dlq/{queueName}/replay` |
| Drain DLQ | `POST /api/v1/admin/messaging/dlq/{queueName}/drain` |

Peek/replay/drain are audit-logged.

---

## 6. Media (images & video)

**Standard flow:** initiate → client PUT to object storage → complete → worker processes → GET asset.

| Feature | Endpoint |
|---------|----------|
| Begin upload (presigned URL) | `POST /api/v1/media/uploads` |
| Complete upload | `POST /api/v1/media/{id}/complete` |
| Get asset + variants | `GET /api/v1/media/{id}` |
| Delete asset | `DELETE /api/v1/media/{id}` |

**Background processing (Worker):**

| Capability | Details |
|------------|---------|
| Images | Thumbnail 96/256px; resize 720/1440; JPEG + WebP |
| Video | Transcode 480p–4K (no upsampling); poster JPEG |
| HLS | Optional `MediaUploadOptions.EnableHls` |
| Virus scan | `IMediaScanner` — ClamAV or Null |
| Magic-byte verify | On upload complete |
| Extension whitelist | Canonical extension per content-type |
| Stale recovery | Cron `StaleMediaRecoveryJob` |
| Storage cleanup | Cron `MediaStorageCleanupJob` |

**Needs config:** `ObjectStorage`, `VideoProcessing` (ffmpeg/ffprobe paths), optional ClamAV in `local/docker-compose.yml`.

---

## 7. Messaging & transactional outbox

| Component | Description |
|-----------|-------------|
| `IMessagePublisher` / `IMessageConsumer` | Abstraction |
| RabbitMQ adapter | Publisher confirms, prefetch, DLQ |
| Transactional outbox | Written in same DB transaction; `OutboxPublisherHostedService` drains |
| Outbox cleanup | Cron `OutboxCleanupJob` |
| Correlation ID | HTTP → outbox → AMQP → worker logs |

**Enable:** `Messaging:RabbitMq:Enabled: true` + connection in appsettings.

**Media event:** `media.uploaded.v1` → `MediaProcessingConsumerService`.

---

## 8. Worker & scheduling

| Job / consumer | Type | Purpose |
|----------------|------|---------|
| `MediaProcessingConsumerService` | Queue | Process media after upload |
| `OutboxPublisherHostedService` | Background | Publish outbox → RabbitMQ |
| `StaleMediaRecoveryJob` | Cron | Stuck Processing assets |
| `MediaStorageCleanupJob` | Cron | Clean orphan object storage |
| `OutboxCleanupJob` | Cron | Outbox processed/abandoned retention |
| `SampleCronJob` | Cron | Dev sample (`Worker:EnableSamples`) |
| `SampleQueueConsumerService` | Queue | Dev sample |

**Scheduling:** `ICronJob` + Quartz or Cronos (`AddWorkerScheduling`).

Run worker: `dotnet run --project src/Backend/Worker`

---

## 9. Realtime (SignalR)

| Feature | Notes |
|---------|-------|
| Hub notifications | `Realtime:Enabled` — maps `/hubs/notifications` |
| JWT via query | `?access_token=` for WebSocket (redacted in logs) |
| Redis backplane | Optional scale-out |

---

## 10. Platform — API, security, ops

| Feature | Notes |
|---------|-------|
| API versioning | `Asp.Versioning` — v1.0 |
| Swagger | Development by default; prod `Swagger:ExposeInProduction` |
| Response envelope | `ApiResponse<T>` + JWT 401/403 envelope |
| Correlation ID | Header `X-Correlation-Id` |
| CORS | `Cors:AllowedOrigins` |
| Rate limiting | `auth`, `media-upload` |
| Security headers | CSP, HSTS, X-Frame-Options, Permissions-Policy, … |
| Forwarded headers | `ReverseProxy:KnownProxies/Networks` |
| AllowedHosts guard | Production requires specific hostname |
| Health | `/health/live`, `/health/ready`, `/health` |
| Health checks | DB, Redis, RabbitMQ, SMTP, Object Storage (per config) |
| OpenTelemetry | Traces/metrics — `AddPlatformOpenTelemetry` |
| Serilog | Structured logging Api + Worker |
| CQRS DbContext | Write `ApplicationDbContext` / Read `ApplicationReadDbContext` |
| Domain events | Interceptor + after-commit queue |
| Data Protection | 2FA secret encryption at rest |
| EF migrations | `Infrastructure/Persistence/Migrations` |

---

## 11. Developer tooling (included in repo)

| Tool | Path | Purpose |
|------|------|---------|
| Apply migrations | `./tools/backend/migrate.sh` | Update local DB |
| Create migration safely | `./tools/backend/migration/new.sh <Name>` | Preflight + generate |
| Rebase migration | `./tools/backend/migration/rebase.sh` | Multi-person teams |
| Drift check | `./tools/backend/migration/check.sh` | Fast / full |
| Git hook | `./tools/backend/git-hooks/install.sh` | Pre-commit migration guard |
| CI migration gate | `.github/workflows/migration-check.yml` | Gitea/GitHub Actions |
| Gitea runner | `tools/ci/gitea-runner.compose.yml` | See `CI_GITEA_SETUP.md` |

---

## 12. Samples (learning / dev only)

| Endpoint | Purpose |
|----------|---------|
| `POST /api/v1/samples/transactions/manual-lock/{userId}` | Demo transaction + lock |
| `POST /api/v1/samples/transactions/empty-roundtrip` | Demo UoW roundtrip |

Not for production use.

---

## 13. Tests (current coverage)

| Project | Scope |
|---------|-------|
| `Domains.Tests` | Aggregate rules |
| `Application.Tests` | Handlers, validators, behaviors |
| `Infrastructure.Tests` | Persistence, interceptors, messaging |
| `Worker.Tests` | Outbox cleanup, media jobs |
| `Api.IntegrationTests` | E2E auth (requires Postgres running) |

Run unit tests: `dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"`

---

## Quick config (local)

| Dependency | Compose service | appsettings key |
|------------|-----------------|-----------------|
| PostgreSQL | `postgres` | `ConnectionStrings:DatabaseConnectionString` |
| Redis | `redis` | `Redis:ConnectionString` |
| RabbitMQ | `rabbitmq` | `Messaging:RabbitMq` |
| MinIO | `minio` | `ObjectStorage` |
| MailHog | `mailhog` | `Smtp` (port 1025) |
| ClamAV | `clamav` | Scanner section in Worker/Api |

Start: `docker compose -f local/docker-compose.yml up -d`

---

## Related documentation

- Onboarding: [`ONBOARDING.md`](./ONBOARDING.md)
- Adding new features: [`BASE_GUIDE.md`](./BASE_GUIDE.md), [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
- Architecture: [`ARCHITECTURE.md`](./ARCHITECTURE.md), [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md)
- Short status matrix: [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md)
