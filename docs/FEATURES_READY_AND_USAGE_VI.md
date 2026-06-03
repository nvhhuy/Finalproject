# Đánh giá feature & hướng dẫn sử dụng (tiếng Việt)

**Cập nhật:** 2026-06-03 (base polish: HTML email, PWA, Web Push, dashboard)  
**Phạm vi:** Toàn bộ monorepo (`src/Backend`, `src/Frontend`, `src/Admin`) — **nền tảng SaaS base**, chưa phải sản phẩm Klassi/EduConnect (lớp học, điểm danh, …).

Tài liệu chi tiết tiếng Anh (endpoint đầy đủ): [`backend/FEATURE_CATALOG.md`](./backend/FEATURE_CATALOG.md), [`frontend/FEATURE_CATALOG.md`](./frontend/FEATURE_CATALOG.md), [`admin/FEATURE_CATALOG.md`](./admin/FEATURE_CATALOG.md).

---

## 1. Kết luận nhanh

| Khía cạnh | Đánh giá |
|-----------|----------|
| **Kiến trúc & code backend** | **Sẵn sàng dùng** — layered .NET 8, Api + Worker, outbox, test 291 case (50 API integration) đều pass |
| **Chạy local end-to-end** | **Được** — sau khi bật Docker, migrate, chạy Api + Worker + Frontend + Admin |
| **Production “bật là chạy”** | **Chưa** — cần secrets, checklist deploy, SMTP/Rabbit/MinIO/DataProtection keys |
| **Sản phẩm Klassi (ClassHub)** | **Chưa có trong `src/`** — chỉ có kế hoạch trong [`educonnect/`](./educonnect/README.md) |

**Verdict:** Repo là **base platform production-viable** (auth, admin, SaaS billing MVP, wallet, support, media pipeline, …). Có thể **dùng ngay để dev/demo và làm nền cho Klassi**; feature giáo dục (class, học viên, lịch, thu phí lớp) cần build thêm theo [`educonnect/TECH_MAPPING.md`](./educonnect/TECH_MAPPING.md).

---

## 2. Cách đọc trạng thái feature

| Nhãn | Ý nghĩa |
|------|---------|
| **Chạy được (local)** | Code + UI/API có; test hoặc flow manual đã cover |
| **Sẵn sàng (code)** | Endpoint/job có; chỉ cần bật config hoặc dependency |
| **Cần cấu hình** | Phải set secret / `Enabled: true` / dịch vụ ngoài (SMTP, Stripe, …) |
| **Một phần** | Có nhưng chưa scale đủ (vd. SignalR cần Redis khi nhiều replica) |
| **Chưa có** | Không implement trong codebase |

---

## 3. Chạy môi trường local (bắt buộc trước khi dùng feature)

### 3.1 Yêu cầu

- .NET 8 SDK, Docker Desktop
- Node **20+** (repo có [`.nvmrc`](../.nvmrc) — chạy `nvm use` trước Frontend/Admin/e2e)

### 3.2 Infra + DB

```bash
# Từ thư mục gốc repo
docker compose -f local/docker-compose.yml up -d
./tools/backend/migrate.sh
```

Dịch vụ local: Postgres `5432`, Redis `6379`, RabbitMQ `5672` (UI `15672`), MinIO `9000`, MailHog SMTP `1025`.

### 3.3 Backend

```bash
dotnet run --project src/Backend/Api      # API (Swagger dev)
dotnet run --project src/Backend/Worker   # Outbox, media, cron, email queue
```

Health: `GET /health/ready` (port theo `launchSettings`, thường `https://localhost:7124`).

### 3.4 Frontend & Admin

```bash
cd src/Frontend && npm install && npm run dev   # http://localhost:3000
cd src/Admin && npm install && npm run dev      # http://localhost:5174
```

Frontend proxy `/api` và `/hubs` về API — gọi API **cùng origin** qua app, không cần CORS phức tạp khi dev.

### 3.5 Tài khoản dev (chỉ Development)

| Vai trò | Email | Mật khẩu |
|---------|-------|-----------|
| Admin | `admin@localhost.com` | `Temp!ChangeMe123` |

Đổi hoặc tắt seed trước staging/production (`Seed:Admin`).

### 3.6 Xác minh tự động

```bash
# Unit + integration (cần Docker cho Testcontainers Postgres)
dotnet test src/Backend

# E2E Playwright (xem docs/test/ONBOARDING.md)
./tools/e2e/run-playwright.sh
```

Kết quả kiểm tra 2026-06-03: **Domains 59**, **Application 104**, **Infrastructure 70**, **Worker 8**, **Api.Integration 50 pass / 1 skip** (media full pipeline khi không bật `INTEGRATION_MEDIA_ENABLED`).

---

## 4. Bảng feature theo module

### 4.1 Xác thực & phiên (Auth)

| Feature | Trạng thái | Ghi chú |
|---------|------------|---------|
| Đăng ký / đăng nhập email | **Chạy được** | Verify email trước khi login; có rate limit |
| Refresh token + phát hiện reuse | **Chạy được** | `POST /api/v1/auth/refresh` |
| Quên / đặt lại mật khẩu | **Chạy được** (local) | Email qua MailHog khi SMTP dev |
| 2FA TOTP | **Chạy được** | Setup + login bước 2 |
| Google Sign-In | **Cần cấu hình** | `Authentication:Google:ClientId` |
| Tenant SSO (OIDC) | **Chạy được** (API) | UI: `/t/[slug]`; test `TenancySsoIntegrationTests` |
| Tắt đăng ký công khai | **Chạy được** | Admin bật/tắt `allow_public_register` |

**Dùng thế nào (end-user):** `/register` → mở MailHog → verify → `/login`. 2FA: `/app/security/2fa`.  
**Admin:** không đăng ký user công khai; quản lý user tại Admin `/users`.

---

### 4.2 Hồ sơ người dùng

| Feature | Trạng thái |
|---------|------------|
| Xem/sửa profile | **Chạy được** — `GET/PUT /api/v1/users/profile`, optimistic `rowVersion` |
| Avatar trên API | **Chưa có** field `avatarUrl` — demo upload qua Media lab |

**Dùng:** đăng nhập Frontend → `/app/profile`.

---

### 4.3 Admin Console

| Feature | Trạng thái | Route Admin |
|---------|------------|-------------|
| Dashboard metrics | **Chạy được** | `/` |
| CRUD user, lock, impersonate, 2FA/password reset | **Chạy được** | `/users`, `/users/:id` |
| Audit log + export CSV | **Chạy được** | `/audit-logs` |
| Settings (đăng ký công khai, backup settings/DB) | **Chạy được** | `/settings` |
| DLQ RabbitMQ peek/replay/drain | **Cần RabbitMQ** | `/messaging` |
| Support tickets (assign, status, reply) | **Chạy được** | `/support` |
| Broadcast thông báo toàn hệ thống | **Chạy được** | `/notifications` |
| Plans / credit packages / tenants / invoices | **Chạy được** | `/plans`, `/credit-packages`, `/tenants`, `/invoices` |

**Dùng:** `http://localhost:5174/login` → admin dev ở mục 3.5. Mọi route cần JWT role **Admin**.

---

### 4.4 Frontend (ứng dụng end-user)

| Feature | Trạng thái | Route |
|---------|------------|-------|
| Landing, about, contact, pricing | **Chạy được** | `/`, `/about`, `/contact`, `/pricing` |
| Auth pages | **Chạy được** | `/login`, `/register`, `/forgot-password`, … |
| Dashboard, profile, 2FA, media lab | **Chạy được** | `/app`, `/app/profile`, … |
| Support tickets | **Chạy được** | `/app/support` |
| Notifications + bell | **Chạy được** | `/app/notifications`; SignalR khi `Realtime:Enabled` |
| Wallet (top-up, mua gói credit, debit demo) | **Chạy được** | `/app/wallet` |
| Subscribe tenant + quản lý org | **Chạy được** | `/app/subscribe`, `/app/tenant` |
| Tenant landing + SSO | **Chạy được** | `/t/[slug]`, `/t/[slug]/app` |

**Header tenant:** sau subscribe, app gửi `X-Tenant-Id` từ `localStorage` cho API tenant-scoped.

**Không có trên Frontend (cố ý):** quản trị user platform, DLQ, CRUD plan — dùng **Admin**.

---

### 4.5 SaaS Billing & Tenancy (MVP)

| Feature | Trạng thái |
|---------|------------|
| Public plans, subscribe, subscription, invoices | **Chạy được** — `BillingIntegrationTests` |
| Đổi plan / hủy subscription | **Chạy được** |
| Platform: CRUD plan, tenant, gán subscription | **Chạy được** (Admin) |
| Entitlements / usage | **Chạy được** (API) |
| Stripe webhook provision tenant | **Chạy được** (test) — **Cần** `Stripe:WebhookSecret` production |
| VNPay / PayOS webhook | **Chạy được** (HMAC verify) — cần secret + sandbox test |
| Dunning job | **Sẵn sàng** — Worker `SubscriptionDunningJob` |

**Flow demo nhanh:**

1. User: `/pricing` hoặc `/app/subscribe` → chọn plan Starter (MVP manual checkout).
2. Vào `/app/tenant` — invite member, domain, SSO config.
3. Admin: tạo tenant/plan nếu cần seed thủ công (`/tenants`, `/plans`).

---

### 4.6 Wallet (cá nhân)

| Feature | Trạng thái |
|---------|------------|
| Số dư, ledger, actions, invoices | **Chạy được** |
| Top-up / purchase package / metered debit | **Chạy được** — `WalletIntegrationTests` + UI `/app/wallet` |

**Dùng:** login → `/app/wallet` → demo top-up hoặc mua credit package (catalog từ `GET /api/v1/public/credit-packages`).

---

### 4.7 Support tickets

| Feature | Trạng thái |
|---------|------------|
| User: tạo, reply, đóng/mở ticket | **Chạy được** |
| Admin: assign, priority, status, internal note | **Chạy được** |

**Dùng:** Frontend `/app/support`; Admin `/support`. Đính kèm qua upload Media → `storageKey` trong message.

---

### 4.8 Media (ảnh & video)

| Feature | Trạng thái |
|---------|------------|
| Initiate → PUT MinIO → complete → GET asset | **Chạy được** khi MinIO + Worker + RabbitMQ bật |
| Xử lý ảnh/video/HLS, ClamAV | **Sẵn sàng** — Worker consumer; ClamAV optional trong compose |
| Gallery list API | **Chưa có** — chỉ lab từng file |

**Dùng:** `/app/media` — upload một asset, chờ Worker xử lý, refresh để xem variant.

**Bật RabbitMQ** (nếu chưa): `Messaging:RabbitMq:Enabled: true` trong appsettings Development.

---

### 4.9 Thông báo & realtime

| Feature | Trạng thái |
|---------|------------|
| In-app list + mark read | **Chạy được** |
| Admin broadcast | **Chạy được** |
| SignalR push | **Một phần** — bật `Realtime:Enabled`; scale multi-pod cần Redis backplane |
| Web Push (trình duyệt) | ✅ | Bật `WebPush:Enabled` + VAPID keys; user bật tại `/app/settings` |
| Push mobile native | **Chưa có** |
| Email transactional | **Sẵn sàng** — outbox → Rabbit → Worker SMTP; production cần SMTP thật |

---

### 4.10 Nền tảng vận hành (API)

| Feature | Trạng thái |
|---------|------------|
| Idempotency-Key (register, media complete, admin writes) | **Chạy được** |
| Feature flags (config + DB) | **Chạy được** |
| Optimistic concurrency → HTTP 409 | **Chạy được** |
| Transactional outbox + RabbitMQ + DLQ admin | **Chạy được** khi Rabbit bật |
| Backup settings / DB (cron + admin UI) | **Chạy được** — restore cần quyền admin + xác nhận |
| Health, OpenTelemetry, Serilog | **Chạy được** |
| Full-text search (OpenSearch) | **Chưa có** |

---

### 4.11 Klassi / EduConnect (sản phẩm)

| Feature roadmap (ClassHub, TutorMatch, …) | Trạng thái |
|-------------------------------------------|------------|
| Quản lý lớp, học viên, lịch, thu phí lớp | **Chưa có** — xem [`educonnect/FEATURE_ROADMAP.md`](./educonnect/FEATURE_ROADMAP.md) |
| Map code sẵn có → feature mới | [`educonnect/TECH_MAPPING.md`](./educonnect/TECH_MAPPING.md) |

---

## 5. Hướng dẫn theo kịch bản người dùng

### 5.1 Giáo viên / học viên (demo end-user)

1. `docker compose` + migrate + Api + Worker.
2. Mở `http://localhost:3000/register` → verify email (MailHog `http://localhost:8025`).
3. Login → `/app` dashboard.
4. Thử: profile, 2FA, support ticket, wallet top-up, subscribe org tại `/app/subscribe`.
5. Tenant public page: `/t/default` (nếu seed tenant `default`).

### 5.2 Vận hành / admin nền tảng

1. Login Admin `admin@localhost.com`.
2. Users: tạo user, lock, impersonate (không impersonate Admin).
3. Plans + Tenants: tạo plan → tenant → gán subscription.
4. Settings: tắt đăng ký công khai; chạy backup thử.
5. Notifications: gửi broadcast → user thấy tại `/app/notifications`.

### 5.3 Developer thêm feature mới

1. Đọc [`backend/BASE_GUIDE.md`](./backend/BASE_GUIDE.md) + pattern module hiện có.
2. Cập nhật catalog tiếng Anh + file này nếu feature **chạy được** và có UI.
3. Thêm test: `Application.Tests` / `Api.IntegrationTests` / Playwright theo [`test/TEST_CATALOG.md`](./test/TEST_CATALOG.md).

---

## 6. Những gì chưa “ready to use” không cần code thêm

| Hạng mục | Lý do |
|----------|--------|
| Deploy production một lệnh | Cần [`deploy/CHECKLIST.md`](./deploy/CHECKLIST.md), secrets, Traefik |
| Google login production | OAuth client + redirect URI |
| Thanh toán thật Stripe/VNPay/PayOS | Webhook URL public + secret + kiểm tra sandbox |
| Media integration test đầy đủ | Set `INTEGRATION_MEDIA_ENABLED=true` + MinIO |
| Tìm kiếm toàn văn | Chưa implement |
| Multi-tenant feature flags | Reserved trong code |
| Sản phẩm Klassi | Sprint 1 chưa bắt đầu theo [`educonnect/BUILD_PLAN.md`](./educonnect/BUILD_PLAN.md) |

---

## 7. Tài liệu liên quan

| Mục đích | File |
|----------|------|
| Tổng quan kiến trúc | [`OVERVIEW.md`](./OVERVIEW.md) |
| Production & scale | [`backend/PRODUCTION_READINESS_REPORT.md`](./backend/PRODUCTION_READINESS_REPORT.md) |
| API đầy đủ | [`backend/FEATURE_CATALOG.md`](./backend/FEATURE_CATALOG.md) |
| Onboarding backend | [`backend/ONBOARDING.md`](./backend/ONBOARDING.md) |
| Kế hoạch Klassi | [`educonnect/README.md`](./educonnect/README.md) |
