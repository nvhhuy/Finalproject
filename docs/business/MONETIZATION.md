# Monetization Plan — Klassi

**Last updated:** 2026-06-02  
**Version:** 2.0  
**Core philosophy:** Free landing page → Tool adoption → Subscription upgrade → Transaction fee scales automatically.

---

## Revenue Stream Overview

| Stream | Module | Who pays | Priority |
|--------|--------|----------|----------|
| **S1: Transaction fee** | ClassHub | Teachers (auto-deducted) | **#1** |
| **S2: Subscription** | ClassHub | Teachers | **#2** |
| **S3: Annual plan** | ClassHub | Teachers | **#3** |
| **S4: Add-ons** | ClassHub | Teachers | #4 |
| **S5: Boost/Ranking** | TutorMatch | Teachers | #5 (Phase 2) |
| **S6: Booking fee** | TutorMatch | Parents | #6 (Phase 2) |

---

## Stream 1 — Transaction Fee (Primary Revenue Engine)

**Model:** Platform collects tuition on behalf of teacher → deducts fee by tier → transfers to teacher's bank account.

**Fee by subscription tier (key incentive to upgrade):**

| Tier | Transaction Fee | Logic |
|------|----------------|-------|
| Free | **2.5%** | Dùng miễn phí → platform lấy nhiều hơn |
| Solo Pro | **1.5%** | Trả subscription → tiết kiệm 1% mỗi giao dịch |
| Center Starter | **1.0%** | Volume lớn → phí thấp nhất |
| Center Growth | **1.0%** | Như Center Starter |

**Why tiered fee drives upgrade:**
```
Giáo viên thu 20 triệu học phí/tháng:
  Free:      20M × 2.5% = 500k phí/tháng
  Solo Pro:  20M × 1.5% = 300k + 349k sub = 649k/tháng
  → Gần bằng nhau nhưng Solo Pro có đầy đủ tính năng hơn
  → Giáo viên tự tính và upgrade
```

**Flow:**
```
Parent → pays tuition via PayOS/VNPay → Platform holds
Platform deducts fee by tier → transfers net to teacher wallet
Teacher requests payout to bank (T+1)
```

**Revenue projection (transaction fee only):**

| Month | Teachers | Avg tuition/month | Volume | Fee rate | Platform earns |
|-------|---------|------------------|--------|----------|----------------|
| 6 | 30 | 15M | 450M | 2.0% avg | **9M VND** |
| 12 | 150 | 18M | 2.7B | 1.8% avg | **49M VND** |
| 18 | 400 | 22M | 8.8B | 1.6% avg | **141M VND** |

---

## Stream 2 — Subscription (Recurring SaaS Revenue)

### Pricing Tiers

| Tier | Monthly | Annual | Key limits | Target |
|------|---------|--------|------------|--------|
| **Free** | 0 | 0 | 1 landing page, 1 lớp, 15 học sinh, no auto billing | Lead magnet — entry vào funnel |
| **Solo Pro** | **349,000 VND** | **3,490,000 VND** | Unlimited lớp, 100 học sinh, full features | Giáo viên/gia sư nghiêm túc |
| **Center Starter** | **990,000 VND** | **9,900,000 VND** | Multi-teacher (3), 300 học sinh, advanced billing | Trung tâm nhỏ |
| **Center Growth** | **1,990,000 VND** | **19,900,000 VND** | Multi-teacher (10+), unlimited, advanced analytics | Trung tâm lớn hơn |

**Annual discount:** ~17% so với monthly → đổi lấy cash upfront.

### What's in Free (entry point — deliberately limited)
```
✅ 1 landing page với template đẹp
✅ 1 lớp học, tối đa 15 học sinh
✅ Form đăng ký học thử cơ bản
✅ Chia sẻ link cho phụ huynh
✅ Klassi branding nhẹ trên trang
❌ Không có auto billing đầy đủ
❌ Không có debt reminder nâng cao
❌ Không có parent portal đầy đủ
❌ Không có attendance report tự động
```

Mục tiêu gói Free: kéo giáo viên vào funnel + tạo viral loop (share link → Klassi branding được thấy), **không phải** cho vận hành lâu dài.

### Upgrade triggers (khi giáo viên tự muốn upgrade)

```
Trigger 1: "Tôi cần mở lớp thứ 2" → bị chặn → upgrade Solo Pro
Trigger 2: "Phụ huynh chưa đóng học phí" → auto reminder chỉ có ở paid
Trigger 3: "Tôi muốn parent portal" → chỉ có từ Solo Pro
Trigger 4: "Transaction fee 2.5% hơi cao" → upgrade để xuống 1.5%
Trigger 5: "Tôi cần thêm giáo viên" → upgrade Center
```

### Revenue projection (subscription only)

| Month | Free | Solo Pro | Center S | Center G | MRR |
|-------|------|---------|---------|---------|-----|
| 6 | 200 | 15 | 3 | 1 | ~10M |
| 12 | 500 | 70 | 15 | 5 | ~41M |
| 18 | 1,000 | 200 | 50 | 15 | ~138M |

---

## Stream 3 — Add-ons

| Add-on | Price | Notes |
|--------|-------|-------|
| **Custom domain** | 99k/tháng | Landing page tại `tencuaban.klassi.vn` → `tencuaban.vn` |
| **Zalo ZNS premium** | 49k/tháng | Tăng hạn mức thông báo Zalo cho trung tâm lớn |
| **Extra admin seat** | 99k/tháng/seat | Thêm nhân viên admin ngoài gói |
| **Onboarding / data import** | 500k one-time | Nhân viên Klassi hỗ trợ import dữ liệu cũ |
| **Branded course page** | 149k/tháng | Remove Klassi branding, custom color/logo |

---

## Stream 4 — TutorMatch: Boost & Booking (Phase 2 — Month 8+)

### Launch criteria (không bán sớm)
- Phải có **500+ parent searches/tháng** trên TutorMatch
- Phải có **data để show ROI** cho teacher (clicks, profile views)
- Expected: **Month 8–10**

### Boost products

| Product | Price/month | What it does |
|---------|-------------|--------------|
| **Top 3 Search** | 200,000 VND | Appear in top 3 for subject + district |
| **Spotlight Badge** | 100,000 VND | Highlighted in search results |
| **Homepage Featured** | 300,000 VND | Featured in "Top Tutors" section |

### Booking fee

| Event | Fee | Who keeps |
|-------|-----|-----------|
| Parent books trial | 30,000 VND | Platform |
| Teacher cancels | 30,000 VND refunded | - |
| Parent cancels >24h | 30,000 VND refunded | - |
| Parent cancels <24h | 30,000 VND kept | Platform (anti-spam) |

---

## Combined Revenue Projection

```
Month 6 (Launch):
  Transaction fee:    9M
  Subscription:      10M
  Add-ons:           1M
  TOTAL MRR:        ~20M VND/tháng (~$800 USD)
  
Month 12 (Traction):
  Transaction fee:   49M
  Subscription:      41M
  Add-ons:           5M
  Boost (early):     5M
  TOTAL MRR:        ~100M VND/tháng (~$4,000 USD)

Month 18 (Growth):
  Transaction fee:  141M
  Subscription:     138M
  Add-ons:          15M
  Boost:            30M
  Booking fee:       5M
  TOTAL MRR:        ~329M VND/tháng (~$13,200 USD)

ARR tháng 18: ~3.9 tỷ VND (~$156K USD)

Operating costs (Month 18):
  Server + infra:   8M
  PayOS/VNPay fees: ~3M (1.5% × transaction volume offset)
  Zalo OA API:      3M
  Misc:             3M
  TOTAL COSTS:      ~17M/tháng

NET PROFIT Month 18: ~312M VND/tháng
```

---

## Breakeven Analysis

```
Fixed monthly costs: ~15–17M VND

Breakeven with subscription only:
  17M / 349k avg = ~49 paying Solo Pro users needed

Breakeven with transaction fee:
  17M / 2.5% = 680M VND tuition volume needed
  = ~34 giáo viên × 20M tuition/tháng each

→ Breakeven đạt được dưới 50 paying users — rất sớm
```

---

## Payment Infrastructure Cost

| Provider | Use case | Platform fee | Klassi net |
|----------|----------|-------------|-----------|
| **PayOS (KLB-1000)** | Primary — QR/bank transfer | ~0% (gói free lấy nhiều lần) | **1.5–2.5% full** |
| **VNPay QR** | Backup | 0.6–1.0% | 0.5–1.9% |
| **VNPay card** | Optional | 1.4–2.5% | 0–1.1% |

**Strategy:** PayOS làm primary vì gần miễn phí → maximize margin. VNPay backup cho các case PayOS down hoặc user prefer thẻ.
