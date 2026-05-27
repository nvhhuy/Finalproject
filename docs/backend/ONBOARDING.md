# Backend onboarding (người mới)

Tài liệu này giúp dev mới **chạy được project trong ~30 phút** và biết **đọc gì tiếp theo**. Base này là **nền production-ready** — bạn fork rồi **rút feature lõi** ra khi cần, không phải viết lại auth/media/messaging từ đầu.

---

## Bạn đang ở đâu trong repo?

```
Finalproject/
├── src/Backend/          ← API, Application, Domains, Infrastructure, Worker
├── src/Frontend/         ← (nếu có) SPA client
├── local/docker-compose.yml
├── tools/backend/        ← migrate.sh, migration scripts, git hooks
├── docs/backend/         ← tài liệu bạn đang đọc
└── .github/workflows/    ← CI (migration drift check)
```

---

## Lộ trình đọc (khuyến nghị)

| Thứ tự | Tài liệu | Thời gian | Mục tiêu |
|--------|----------|-----------|----------|
| 1 | **File này** (`ONBOARDING.md`) | 15 phút | Chạy local + hiểu bức tranh |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 20 phút | Biết base đã có gì |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) + [`MODULE_STRUCTURE.md`](./MODULE_STRUCTURE.md) | 30 phút | Layer, folder, bounded context |
| 4 | [`BASE_GUIDE.md`](./BASE_GUIDE.md) | 45 phút | Flow thêm feature cụ thể |
| 5 | [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) | Khi cần | Auth, cache, worker, logging chi tiết |
| 6 | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) | Trước khi đụng DB schema | Team nhiều người |
| 7 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Khi code với AI | Quy tắc bắt buộc |

**Không cần đọc hết một lúc.** Ngày 1: mục 1–3; tuần 1: thêm 4–6.

---

## Day 1 — checklist

### 1. Cài đặt

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Postgres, Redis, RabbitMQ, MinIO, …)
- `dotnet tool install --global dotnet-ef` (EF CLI)
- Git hooks (một lần): `./tools/backend/git-hooks/install.sh`

### 2. Infrastructure local

Từ **repo root**:

```bash
docker compose -f local/docker-compose.yml up -d
```

Kiểm tra: Postgres `5432`, Redis `6379`, RabbitMQ `5672` / UI `15672`, MinIO `9000`.

### 3. Cấu hình API

File mẫu: `src/Backend/Api/appsettings.Development.json`

- Connection string trỏ `localhost:5432`, database `finalproject`
- JWT secret chỉ dùng dev (production phải đổi)
- `Database:ApplyPendingMigrations: true` — API tự migrate khi start (dev)

### 4. Chạy API + Worker

```bash
# Terminal 1 — API
dotnet run --project src/Backend/Api

# Terminal 2 — Worker (media, outbox, cron)
dotnet run --project src/Backend/Worker
```

### 5. Kiểm tra nhanh

- Swagger: `https://localhost:<port>/swagger` (port xem log khi start)
- Health: `GET /health/ready`
- Đăng ký / login qua Swagger hoặc Postman — xem [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) mục Auth

### 6. Tests

```bash
cd src/Backend
dotnet test --filter "FullyQualifiedName!~Api.IntegrationTests"
```

Integration tests cần Postgres đang chạy:

```bash
dotnet test Tests/Api.IntegrationTests
```

---

## Kiến trúc một câu

```
HTTP Request
    → Api (Controller)
    → MediatR (Command/Query + Validation + AdminAuth behaviors)
    → Application Handler
    → Domain (rules) + Infrastructure (EF, cache, email, storage, …)
    → IUnitOfWork.SaveChangesAsync (một lần / request khi [Transactional])
```

**Quy tắc vàng:**

- Controller **mỏng** — không business logic.
- Ghi DB qua **write repository** + `ApplicationDbContext`.
- Đọc qua **read repository** / `IApplicationReadDbContext` (no-tracking).
- Mutation quan trọng: `[Transactional]` trên action.

---

## Tôi muốn làm gì? → Đi đâu

| Mục tiêu | Bắt đầu từ |
|----------|------------|
| Xem API có những gì | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) |
| Thêm API endpoint mới | [`BASE_GUIDE.md`](./BASE_GUIDE.md) § thêm feature |
| Đổi bảng DB | [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md) + `migration/new.sh` |
| Debug auth / JWT | [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) § Authentication |
| Bật RabbitMQ / outbox | [`CODEBASE_INCLUDED_AND_USAGE_GUIDE.md`](./CODEBASE_INCLUDED_AND_USAGE_GUIDE.md) |
| Upload ảnh/video | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) § Media + Worker |
| Setup CI Gitea | [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) |
| Biết còn thiếu gì cho prod | [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md) |

---

## Thêm feature mới (tóm tắt 7 bước)

1. **Domain** — entity/aggregate trong `Domains/Modules/<Feature>/`
2. **Application** — command/query + handler + validator trong `Application/Modules/<Feature>/`
3. **Contracts** — interface repository/service trong `Application/Contracts/`
4. **Infrastructure** — EF config, repository trong `Infrastructure/Modules/<Feature>/`
5. **Migration** — `./tools/backend/migration/new.sh AddMyFeature`
6. **Api** — controller + models trong `Api/Controllers`, `Api/Models`
7. **Test** — handler test trong `Application.Tests`

Chi tiết + ví dụ: [`BASE_GUIDE.md`](./BASE_GUIDE.md).

---

## Cheat sheet lệnh

```bash
# Infra
docker compose -f local/docker-compose.yml up -d

# DB migrate
./tools/backend/migrate.sh

# Tạo migration
./tools/backend/migration/new.sh AddSomething

# Check migration trước commit
./tools/backend/migration/check.sh --fast

# Build + test
dotnet build src/Backend
dotnet test src/Backend --filter "FullyQualifiedName!~Api.IntegrationTests"
```

---

## Vai trò từng project (nhắc nhanh)

| Project | Bạn sửa khi… |
|---------|----------------|
| `Api` | Thêm route, middleware, auth policy |
| `Application` | Logic use-case, validation |
| `Domains` | Rule nghiệp vụ, aggregate |
| `Infrastructure` | DB, email, S3, RabbitMQ |
| `Worker` | Job nền, consumer queue |
| `Tests/*` | Unit/integration |

---

## Sau onboarding

- Tham gia PR: đảm bảo migration check + tests pass.
- Feature lớn: đọc [`EXTENSIBILITY_AND_GAPS.md`](./EXTENSIBILITY_AND_GAPS.md) trước khi hứa multi-tenant / billing v.v.
- Cập nhật [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) khi merge feature mới vào base.

**Hub tài liệu:** [`../README.md`](../README.md)
