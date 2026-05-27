# Frontend onboarding (người mới)

Giúp dev mới **chạy Frontend local trong ~15 phút** và biết **đọc gì tiếp theo**. Đây là app **end-user** (đăng ký, profile, media) — khác Admin Console.

---

## Bạn đang ở đâu trong repo?

```
Finalproject/
├── src/Frontend/           ← Next.js app (file này)
├── src/Backend/Api/        ← API
├── src/Admin/              ← Admin SPA (operators)
├── local/docker-compose.yml
└── docs/frontend/          ← tài liệu bạn đang đọc
```

---

## Lộ trình đọc (khuyến nghị)

| Thứ tự | Tài liệu | Thời gian | Mục tiêu |
|--------|----------|-----------|----------|
| 1 | **File này** | 15 phút | Chạy local + đăng nhập |
| 2 | [`FEATURE_CATALOG.md`](./FEATURE_CATALOG.md) | 15 phút | Routes + API đã có |
| 3 | [`ARCHITECTURE.md`](./ARCHITECTURE.md) | 20 phút | Folder, auth, proxy |
| 4 | [`COMMON_LIBRARIES.md`](./COMMON_LIBRARIES.md) | 10 phút | `formatDate`, `@/lib/shared` |
| 5 | [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md) | Khi thêm feature | Quy trình mở rộng |
| 6 | [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md) | Khi dùng AI | Quy tắc code Frontend |

**Không cần đọc hết một lúc.** Ngày 1: mục 1–4; khi sửa code: thêm 5–6.

---

## Day 1 — checklist

### 1. Cài đặt

- [Node.js ≥ 20.19](https://nodejs.org/) (Next.js 15 yêu cầu)
- Backend API + Postgres (xem [`docs/backend/ONBOARDING.md`](../backend/ONBOARDING.md))
- Docker: `docker compose -f local/docker-compose.yml up -d postgres` (từ repo root)

### 2. Chạy Backend (HTTPS)

```bash
cd src/Backend/Api
dotnet run --launch-profile https
```

Swagger: `https://localhost:7124/swagger`

### 3. Cấu hình Frontend

```bash
cd src/Frontend
cp .env.example .env.local   # tùy chọn
npm install
```

| Biến | Mục đích |
|------|----------|
| `API_PROXY_TARGET` | Server proxy → API (mặc định `https://localhost:7124`) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Google Sign-In (tùy chọn) |
| `NEXT_PUBLIC_API_BASE_URL` | Production: gọi API trực tiếp (dev để trống = proxy `/api`) |

### 4. Chạy dev

```bash
npm run dev
```

Mở [http://localhost:3000](http://localhost:3000).

Browser gọi **`/api/v1/...`** trên cùng origin; Next route `app/api/[...path]/route.ts` forward tới backend (tránh CORS + self-signed cert).

### 5. Đăng nhập (Development seed)

| Field | Value |
|-------|-------|
| Email | `admin@localhost.com` |
| Password | `Temp!ChangeMe123` |

User seed có thể là Admin role — vẫn login được trên Frontend (khác Admin app chỉ cho role Admin).

### 6. Kiểm tra nhanh

- [ ] `/` landing — đổi ngôn ngữ EN / VI / 中文
- [ ] `/login` → `/app` dashboard
- [ ] `/app/profile` — sửa tên, resend verification
- [ ] `/app/security/2fa` — begin setup + QR
- [ ] `/app/media` — upload file, poll status
- [ ] `/register`, `/forgot-password` (flow UI)

### 7. Typecheck

```bash
npm run typecheck
```

---

## Mental model

| Layer | Vai trò |
|-------|---------|
| `app/**/page.tsx` | Route UI — mỏng, compose components |
| `lib/features/*` | Gọi API theo domain (auth, users, media) |
| `lib/api/http.ts` | Envelope, JWT, refresh, idempotency |
| `lib/auth/*` | Session, `RequireAuth`, hydrate profile |
| `lib/shared/*` | Format date, empty cells, pagination helpers |
| `i18n/locales/*.json` | Mọi chuỗi UI (en, vi, zh) |

**Không** gọi `fetch` trực tiếp tới `https://localhost:7124` từ browser trong dev — dùng path `/api/v1/...`.

---

## Khi gặp lỗi thường gặp

| Triệu chứng | Cách xử lý |
|-------------|------------|
| CORS / `Failed to fetch` | Backend chưa chạy; hoặc xóa `NEXT_PUBLIC_API_BASE_URL` trong `.env.local` để dùng proxy |
| `Internal Server Error` trên `/api` | Restart Frontend sau khi pull; kiểm tra `API_PROXY_TARGET` |
| Hydration `lang` mismatch | Đổi ngôn ngữ một lần (ghi cookie); xem `LocaleSync` |
| Login 401 | Sai password; Postgres chưa seed; xem Swagger login |

---

## Tiếp theo

- Thêm màn product: [`EXTENSIBILITY_GUIDE.md`](./EXTENSIBILITY_GUIDE.md)
- AI/Cursor: [`.cursor/rules/frontend.mdc`](../../.cursor/rules/frontend.mdc) + [`LLM_FEATURE_IMPLEMENTATION_RULES.md`](./LLM_FEATURE_IMPLEMENTATION_RULES.md)
