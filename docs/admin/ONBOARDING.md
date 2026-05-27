# Admin onboarding (người mới)

Giúp dev mới **chạy Admin local trong ~15 phút** và biết **đọc gì tiếp theo**. Admin là SPA nội bộ — không thay thế Swagger, nhưng là cách vận hành hàng ngày (users, audit, DLQ, settings).

---

## Bạn đang ở đâu trong repo?

```
Finalproject/
├── src/Admin/              ← Admin Console (Vite + React)
├── src/Backend/Api/        ← API mà Admin gọi
├── local/docker-compose.yml
└── docs/admin/             ← tài liệu bạn đang đọc
```

---

## Lộ trình đọc (khuyến nghị)

| Thứ tự | Tài liệu | Thời gian | Mục tiêu |
|--------|----------|-----------|----------|
| 1 | **File này** | 15 phút | Chạy local + đăng nhập |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 15 phút | Biết có những màn / API gì |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | 20 phút | Folder, auth, data flow |
| 4 | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) | 15 phút | Helper dùng chung (date, toast, …) |
| 5 | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) | Khi thêm feature | Quy trình mở rộng |
| 6 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Khi dùng AI | Quy tắc code Admin |

**Không cần đọc hết một lúc.** Ngày 1: mục 1–3; khi sửa code: thêm 4–6.

---

## Day 1 — checklist

### 1. Cài đặt

- [Node.js ≥ 20.19](https://nodejs.org/) (khớp `package.json` engines)
- Backend API chạy được (xem [`docs/backend/ONBOARDING.md`](../backend/ONBOARDING.md))
- Docker local (Postgres, …): `docker compose -f local/docker-compose.yml up -d` từ repo root

### 2. Cấu hình Admin

```bash
cd src/Admin
cp .env.example .env   # tùy chọn — chỉnh proxy nếu API port khác
npm install
```

| Biến | Mục đích |
|------|----------|
| `VITE_API_PROXY_TARGET` | Target cho Vite proxy `/api` → backend (mặc định `https://localhost:7124`) |
| `VITE_API_BASE_URL` | Production: origin API tuyệt đối (để trống = same-origin `/api`) |

### 3. Chạy dev

```bash
npm run dev
```

Mở [http://localhost:5174](http://localhost:5174).

Backend `Cors:AllowedOrigins` phải có `http://localhost:5174` (đã có trong `appsettings.Development.json`).

### 4. Đăng nhập (Development)

| Field | Value |
|-------|-------|
| Email | `admin@localhost` |
| Password | `Temp!ChangeMe123` |

Chỉ tài khoản role **Admin** hoàn tất được flow đăng nhập. Nếu bật 2FA trên user admin, làm theo màn hình xác thực.

### 5. Kiểm tra nhanh

- [ ] Dashboard load metrics (hoặc thông báo lỗi nếu API tắt)
- [ ] Users — list, filter, mở user detail
- [ ] Đổi ngôn ngữ (header / sidebar / login) — EN / VI / 中文
- [ ] Settings — toggle public registration
- [ ] Audit logs — filter theo `targetUserId` từ user detail

### 6. Build & lint

```bash
npm run build
npm run lint
```

---

## Cấu trúc source (tóm tắt)

```
src/Admin/src/
├── app/              # App, router, providers (Query, auth hydrate, Toaster)
├── config/           # env (API_V1)
├── features/         # Màn hình theo domain
│   ├── auth/
│   ├── dashboard/
│   ├── users/
│   ├── settings/
│   ├── audit/
│   └── messaging/    # DLQ
├── i18n/             # en, vi, zh + config
└── shared/
    ├── api/          # client, admin-api, auth-api
    ├── components/   # ui, layout, patterns
    ├── constants/    # query-keys, styles
    ├── hooks/
    ├── lib/          # format, validation, toast, …
    ├── stores/
    └── types/        # DTO mirror backend
```

Chi tiết: [`ARCHITECTURE.md`](./ARCHITECTURE.md).

---

## Quy ước làm việc hàng ngày

1. **Feature mới** → đọc [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) trước khi code.
2. **Chuỗi UI** → thêm key vào **cả 3** file `i18n/locales/{en,vi,zh}.json`.
3. **Ngày/giờ** → dùng `@/shared/lib` (`formatDateOrDash`, …), không gọi `toLocaleDateString()` trực tiếp trong page.
4. **API mutation** → `idempotent: true` qua `admin-api`; user update/delete cần `rowVersion` (xem backend concurrency doc).
5. **Query cache** → dùng `queryKeys` từ `@/shared/constants/query-keys`.

---

## Troubleshooting

| Triệu chứng | Gợi ý |
|-------------|--------|
| CORS error | Thêm origin Admin vào backend `Cors:AllowedOrigins` |
| 401 sau vài phút | Refresh token trong sessionStorage; kiểm tra API `/auth/refresh` |
| SSL proxy lỗi | Thử `VITE_API_PROXY_TARGET=http://localhost:5076` nếu API HTTP |
| Metrics / list trống | API chưa chạy hoặc DB chưa seed |
| Concurrency conflict toast | User bị sửa song song — refresh trang, lấy `rowVersion` mới |

---

## Tiếp theo

- Thêm màn hình: [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md)
- Danh sách tính năng hiện có: [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md)
- Code với AI: [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) + rule `.cursor/rules/admin.mdc`
