# Backend feature catalog

Danh sách **tính năng đã có sẵn trong base** — dùng được ngay sau khi cấu hình local/prod. Đây là “menu” chính thức; khi thêm feature mới, cập nhật file này.

**API version:** `v1.0`  
**Base path:** `api/v1/...` (ví dụ `POST /api/v1/auth/login`)

---

## Cách đọc bảng

| Cột | Ý nghĩa |
|-----|---------|
| **Sẵn sàng** | Có code + endpoint/job; chỉ cần bật config / dependency |
| **Cần config** | Phải set connection string, secret, hoặc `Enabled: true` |
| **Dev only** | Chỉ nên bật trong Development |

---

## 1. Authentication & session

| Tính năng | Sẵn sàng | Endpoint / luồng |
|-----------|----------|------------------|
| Đăng ký (local) | Có | `POST /api/v1/auth/register` |
| Gửi lại email xác minh | Có | `POST /api/v1/auth/resend-verification` |
| Xác minh email (token) | Có | `POST /api/v1/auth/verify-email` |
| Đăng nhập email/password | Có | `POST /api/v1/auth/login` |
| Refresh token (rotation) | Có | `POST /api/v1/auth/refresh` |
| Phát hiện reuse refresh token | Có | Tự động trong handler refresh; revoke toàn bộ session + audit |
| Google sign-in | Cần config | `POST /api/v1/auth/google` — `Authentication:Google:ClientId` |
| Quên mật khẩu | Cần SMTP | `POST /api/v1/auth/forgot-password` |
| Reset mật khẩu (token) | Có | `POST /api/v1/auth/reset-password` |
| 2FA — bắt đầu setup | Có (đã login) | `POST /api/v1/auth/2fa/begin` |
| 2FA — xác nhận bật | Có | `POST /api/v1/auth/2fa/confirm` |
| 2FA — hoàn tất login | Có | `POST /api/v1/auth/login/2fa` |
| Chặn đăng ký công khai | Có | App setting `allow_public_register` (admin API) |
| Lockout đăng nhập sai | Có | Cache-based tracker; không cần endpoint riêng |
| JWT access token | Có | Claim: `sub`, `role`, `two_factor` (đã trim PII) |
| Rate limit auth | Có | Policy `auth` trên `AuthController` |

**Bảo mật liên quan:** PBKDF2 600k iterations, password complexity policy, anti-enumeration login, 2FA brute-force + replay protection.

---

## 2. User (self-service)

| Tính năng | Sẵn sàng | Endpoint |
|-----------|----------|----------|
| Xem profile | Có (JWT) | `GET /api/v1/users/profile` |
| Sửa profile | Có | `PUT /api/v1/users/profile` |

---

## 3. Admin — quản lý user

**Yêu cầu:** JWT role `Admin` (`AuthorizationPolicies.AdminOnly`).

| Tính năng | Endpoint |
|-----------|----------|
| Danh sách user | `GET /api/v1/admin/users` |
| Tạo user | `POST /api/v1/admin/users` |
| Sửa user (tên, email, role, …) | `PUT /api/v1/admin/users/{userId}` |
| Xóa mềm user | `DELETE /api/v1/admin/users/{userId}` |
| Khóa tài khoản | `POST /api/v1/admin/users/{userId}/lock` |
| Mở khóa | `POST /api/v1/admin/users/{userId}/unlock` |
| Impersonate (cấp token admin-as-user) | `POST /api/v1/admin/users/{userId}/impersonate` |
| Reset 2FA | `POST /api/v1/admin/users/{userId}/2fa/reset` |
| Reset mật khẩu (admin) | `POST /api/v1/admin/users/{userId}/password/reset` |
| Đánh dấu email đã verify | `POST /api/v1/admin/users/{userId}/email/verify` |
| Gửi lại email verify | `POST /api/v1/admin/users/{userId}/email/resend-verification` |
| Revoke tất cả session | `POST /api/v1/admin/users/{userId}/sessions/revoke` |

Mọi thao tác ghi audit qua `IAdminAuditLogWriter` (cùng transaction với user write).

---

## 4. Admin — settings, audit, metrics

| Tính năng | Endpoint |
|-----------|----------|
| Đọc toggle đăng ký công khai | `GET /api/v1/admin/settings/allow-public-register` |
| Bật/tắt đăng ký công khai | `PUT /api/v1/admin/settings/allow-public-register` |
| Xem audit log (phân trang) | `GET /api/v1/admin/audit-logs` |
| Dashboard metrics (user counts, …) | `GET /api/v1/admin/metrics/dashboard` |

**App settings:** bảng `AppSettings` + `IAppSettingsService` — có thể mở rộng thêm key mới theo pattern hiện có.

---

## 5. Admin — messaging (DLQ)

**Yêu cầu:** RabbitMQ bật + queue đã cấu hình DLQ.

| Tính năng | Endpoint |
|-----------|----------|
| Peek dead-letter messages | `GET /api/v1/admin/messaging/dlq/{queueName}/peek` |
| Replay DLQ | `POST /api/v1/admin/messaging/dlq/{queueName}/replay` |
| Drain DLQ | `POST /api/v1/admin/messaging/dlq/{queueName}/drain` |

Có audit log cho peek/replay/drain.

---

## 6. Media (ảnh & video)

**Luồng chuẩn:** initiate → client PUT lên object storage → complete → worker xử lý → GET asset.

| Tính năng | Endpoint |
|-----------|----------|
| Bắt đầu upload (presigned URL) | `POST /api/v1/media/uploads` |
| Hoàn tất upload | `POST /api/v1/media/{id}/complete` |
| Lấy asset + variants | `GET /api/v1/media/{id}` |
| Xóa asset | `DELETE /api/v1/media/{id}` |

**Xử lý nền (Worker):**

| Khả năng | Chi tiết |
|----------|----------|
| Ảnh | Thumbnail 96/256px; resize 720/1440; JPEG + WebP |
| Video | Transcode 480p–4K (không upsample); poster JPEG |
| HLS | Tùy chọn `MediaUploadOptions.EnableHls` |
| Quét virus | `IMediaScanner` — ClamAV hoặc Null |
| Magic-byte verify | Khi complete upload |
| Extension whitelist | Canonical extension theo content-type |
| Stale recovery | Cron `StaleMediaRecoveryJob` |
| Storage cleanup | Cron `MediaStorageCleanupJob` |

**Cần config:** `ObjectStorage`, `VideoProcessing` (ffmpeg/ffprobe paths), tùy chọn ClamAV trong `local/docker-compose.yml`.

---

## 7. Messaging & transactional outbox

| Thành phần | Mô tả |
|------------|--------|
| `IMessagePublisher` / `IMessageConsumer` | Abstraction |
| RabbitMQ adapter | Publisher confirms, prefetch, DLQ |
| Transactional outbox | Ghi cùng DB transaction; `OutboxPublisherHostedService` drain |
| Outbox cleanup | Cron `OutboxCleanupJob` |
| Correlation ID | HTTP → outbox → AMQP → worker logs |

**Bật:** `Messaging:RabbitMq:Enabled: true` + connection trong appsettings.

**Event media:** `media.uploaded.v1` → `MediaProcessingConsumerService`.

---

## 8. Worker & scheduling

| Job / consumer | Loại | Mục đích |
|----------------|------|----------|
| `MediaProcessingConsumerService` | Queue | Xử lý media sau upload |
| `OutboxPublisherHostedService` | Background | Publish outbox → RabbitMQ |
| `StaleMediaRecoveryJob` | Cron | Asset Processing treo |
| `MediaStorageCleanupJob` | Cron | Dọn object storage orphan |
| `OutboxCleanupJob` | Cron | Retention outbox processed/abandoned |
| `SampleCronJob` | Cron | Dev sample (`Worker:EnableSamples`) |
| `SampleQueueConsumerService` | Queue | Dev sample |

**Scheduling:** `ICronJob` + Quartz hoặc Cronos (`AddWorkerScheduling`).

Chạy worker: `dotnet run --project src/Backend/Worker`

---

## 9. Realtime (SignalR)

| Tính năng | Ghi chú |
|-----------|---------|
| Hub notifications | `Realtime:Enabled` — map `/hubs/notifications` |
| JWT qua query | `?access_token=` cho WebSocket (đã redact trong logs) |
| Redis backplane | Tùy chọn scale-out |

---

## 10. Platform — API, security, ops

| Tính năng | Ghi chú |
|-----------|---------|
| API versioning | `Asp.Versioning` — v1.0 |
| Swagger | Development mặc định; prod `Swagger:ExposeInProduction` |
| Response envelope | `ApiResponse<T>` + JWT 401/403 envelope |
| Correlation ID | Header `X-Correlation-Id` |
| CORS | `Cors:AllowedOrigins` |
| Rate limiting | `auth`, `media-upload` |
| Security headers | CSP, HSTS, X-Frame-Options, Permissions-Policy, … |
| Forwarded headers | `ReverseProxy:KnownProxies/Networks` |
| AllowedHosts guard | Production bắt buộc hostname cụ thể |
| Health | `/health/live`, `/health/ready`, `/health` |
| Health checks | DB, Redis, RabbitMQ, SMTP, Object Storage (theo config) |
| OpenTelemetry | Traces/metrics — `AddPlatformOpenTelemetry` |
| Serilog | Structured logging Api + Worker |
| CQRS DbContext | Write `ApplicationDbContext` / Read `ApplicationReadDbContext` |
| Domain events | Interceptor + after-commit queue |
| Data Protection | 2FA secret encryption at rest |
| EF migrations | `Infrastructure/Persistence/Migrations` |

---

## 11. Developer tooling (đã có trong repo)

| Tool | Đường dẫn | Mục đích |
|------|-----------|----------|
| Apply migrations | `./tools/backend/migrate.sh` | Cập nhật DB local |
| Tạo migration an toàn | `./tools/backend/migration/new.sh <Name>` | Preflight + generate |
| Rebase migration | `./tools/backend/migration/rebase.sh` | Team nhiều người |
| Drift check | `./tools/backend/migration/check.sh` | Fast / full |
| Git hook | `./tools/backend/git-hooks/install.sh` | Pre-commit migration guard |
| CI migration gate | `.github/workflows/migration-check.yml` | Gitea/GitHub Actions |
| Gitea runner | `tools/ci/gitea-runner.compose.yml` | Xem `CI_GITEA_SETUP.md` |

---

## 12. Samples (chỉ học / dev)

| Endpoint | Mục đích |
|----------|----------|
| `POST /api/v1/samples/transactions/manual-lock/{userId}` | Demo transaction + lock |
| `POST /api/v1/samples/transactions/empty-roundtrip` | Demo UoW roundtrip |

Không dùng trong production.

---

## 13. Tests (coverage hiện có)

| Project | Phạm vi |
|---------|--------|
| `Domains.Tests` | Aggregate rules |
| `Application.Tests` | Handlers, validators, behaviors |
| `Infrastructure.Tests` | Persistence, interceptors, messaging |
| `Worker.Tests` | Outbox cleanup, media jobs |
| `Api.IntegrationTests` | E2E auth (cần Postgres chạy) |

Chạy unit: `dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"`

---

## Config nhanh (local)

| Dependency | Compose service | appsettings key |
|------------|-----------------|-----------------|
| PostgreSQL | `postgres` | `ConnectionStrings:DatabaseConnectionString` |
| Redis | `redis` | `Redis:ConnectionString` |
| RabbitMQ | `rabbitmq` | `Messaging:RabbitMq` |
| MinIO | `minio` | `ObjectStorage` |
| MailHog | `mailhog` | `Smtp` (port 1025) |
| ClamAV | `clamav` | Scanner section trong Worker/Api |

Khởi động: `docker compose -f local/docker-compose.yml up -d`

---

## Tài liệu liên quan

- Onboarding: [`ONBOARDING.md`](./ONBOARDING.md)
- Thêm feature mới: [`BASE_GUIDE.md`](./BASE_GUIDE.md), [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
- Kiến trúc: [`ARCHITECTURE.md`](./ARCHITECTURE.md), [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md)
- Matrix trạng thái ngắn: [`FEATURE_BASELINE_MATRIX.md`](./FEATURE_BASELINE_MATRIX.md)
