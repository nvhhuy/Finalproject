# Build Plan — Klassi MVP + Phase 1

**Last updated:** 2026-06-02  
**Version:** 2.0 — Updated strategy: Tool-first, Marketplace-after  
**Timeline:** 5 months (Month 1–5) → Public launch Month 6  
**Approach:** Solo founder + AI-assisted (Cursor) vibe coding  
**Base:** Existing codebase (auth, tenant, billing, wallet, notifications, media — all reused)

---

## Strategy: Tool-first, Marketplace-after

```
Old plan: Build ClassHub + TutorMatch in parallel
New plan: ClassHub first → earn money → seed TutorMatch supply → open marketplace gradually

Why:
  → Marketplace cold-start is the hardest problem
  → ClassHub solves a clear pain → easier to sell → revenue sooner
  → ClassHub users naturally become TutorMatch supply
  → No need to solve both sides at once
```

---

## Development Philosophy

```
Rule 1: Landing page free = entry point, not class management
Rule 2: Each sprint ships something real users can touch
Rule 3: Reuse existing patterns — never reinvent infrastructure
Rule 4: Feature flags everything — ship to prod, enable for beta users
Rule 5: Talk to 3 real users before building each sprint's features
Rule 6: Marketing happens in parallel — not after building
```

---

## Month 1 — Landing Page + Class Core

**Goal:** Teacher has a professional page to share + can create first class

### Sprint 1 (Week 1–2): Teacher Landing Page (Free Tier Entry)

**Why this first:** Immediate value for teacher on day 1. Share link → parent sees it → Klassi gets organic exposure.

**Deliverables:**
- [ ] `TeacherPage` entity (slug, template, heroImage, bio, subjects, priceRange, contactCTA, tenantId)
- [ ] 3 templates (minimal, modern, colorful) — template-driven, NOT drag-and-drop
- [ ] Editable sections: hero, about, courses list, testimonials, FAQ, contact CTA
- [ ] CTA types: "Đăng ký học thử", "Đăng ký khóa", "Nhắn Zalo", "Giữ chỗ cọc"
- [ ] Public URL: `klassi.vn/[teacher-slug]`
- [ ] SEO metadata (title, description, OG tags)
- [ ] Mobile-first responsive
- [ ] Klassi branding badge (removed on paid add-on)
- [ ] Lead capture form → saved as `Inquiry` record for teacher

**Reuse from base:**
- Media upload for hero image/avatar (already built)
- Tenant slug pattern (already in tenant system)
- Form submission → notification system

---

### Sprint 2 (Week 3–4): Class & Student Management

**Deliverables:**
- [ ] `Class` entity (name, subject, sessionType: offline/online/hybrid, schedule, monthlyFeeVND, maxStudents, tenantId)
- [ ] `ClassStudent` (classId, studentName, parentEmail, parentPhone, enrolledAt)
- [ ] Commands: `CreateClassCommand`, `UpdateClassCommand`, `AddStudentCommand`
- [ ] Queries: `ListMyClassesQuery`, `GetClassDetailQuery`, `ListClassStudentsQuery`
- [ ] API: `ClassesController`, `StudentsController`
- [ ] Frontend: Classes list + create/edit form + student management
- [ ] Support session types: **offline / online / hybrid** (set per class)
- [ ] Online class: optional Zoom/Meet link attached per session

**Feature flag:** `classhub.enabled = false` until beta

---

## Month 2 — Attendance + Tuition Collection

**Goal:** Core paid-tier features that justify Solo Pro 349k/month

### Sprint 3 (Week 5–6): Attendance Tracking

**Deliverables:**
- [ ] `ClassSession` entity (classId, date, type: offline/online/hybrid, topic, onlineLink, notes)
- [ ] `AttendanceRecord` (sessionId, studentId, status: Present/Absent/Late, note)
- [ ] Commands: `CreateSessionCommand`, `RecordAttendanceCommand`
- [ ] Auto-email parent after attendance saved (reuse outbox pattern)
- [ ] Attendance report per student (monthly summary)
- [ ] Make-up class tracking (`MakeupSession`)

**Email template:**
```
Subject: [Tên con] - Điểm danh buổi [Môn] hôm nay
Body:
  Buổi học: [loại: Trực tiếp / Online / Hybrid]
  Link lớp: [nếu online]
  Trạng thái: ✅ Có mặt / ❌ Vắng / ⚠️ Trễ
  Nhận xét: [note]
  Xem chi tiết: [parent portal link]
```

---

### Sprint 4 (Week 7–8): Tuition Collection + Debt Tracker

**This is the #1 reason teachers upgrade to Solo Pro.**

**Deliverables:**
- [ ] `TuitionRecord` (classId, studentId, month, amountVND, dueDate, status, paidAt, transactionId)
- [ ] Auto-generate monthly tuition via Quartz job (1st of month)
- [ ] PayOS payment link per student per month
- [ ] **Debt reminder auto-send:** D-3 before due + D+1 if overdue
- [ ] **Debt dashboard:** teacher sees at a glance who paid / who owes / total outstanding
- [ ] Transaction fee deduction (2.5% free / 1.5% paid) on webhook
- [ ] Cash payment recording (manual mark as paid)
- [ ] Teacher payout to bank (from wallet)

**Reuse from base:**
- VNPay/PayOS webhook handlers (already built)
- Wallet system (already built)
- Quartz scheduler (already configured)
- IdempotencyFilter for payment webhooks

---

## Month 3 — Parent Portal + Anonymous Board

**Goal:** Lock-in parents into the Klassi ecosystem → switching cost skyrockets

### Sprint 5 (Week 9–10): Parent Portal (Magic Link)

**Deliverables:**
- [ ] `ParentPortalToken` (studentId, token UUID, expiresAt 90 days)
- [ ] Public route: `/parent/[token]` — no auth required
- [ ] Parent view: attendance history, tuition status + pay, upcoming schedule, session notes
- [ ] Pay tuition button directly in portal
- [ ] Auto-send portal link in first attendance email
- [ ] **Parent timeline:** full history of attendance + payments + teacher notes

**Parent timeline is key:** Parents see everything in one place → they expect it → teacher can't leave Klassi without disrupting parents.

---

### Sprint 6 (Week 11–12): Anonymous Q&A Board

**Differentiator** vs TutorBird/Teachworks — no international competitor has this for Vietnam context.

**Deliverables:**
- [ ] `AnonQuestion` (classId, content, anonymousAlias, askedAt, upvoteCount)
- [ ] `AnonAnswer` (questionId, teacherId, content, answeredAt)
- [ ] Class join code for student access (no login needed)
- [ ] Student posts question anonymously → teacher sees all → replies inline
- [ ] Upvote system (IP-based, anti-spam)
- [ ] Answered questions visible to all class members
- [ ] Rate limiting per IP

**Note:** Feature flags this behind `classhub.anon-board = false` until Sprint 6 done.

---

## Month 4 — Course Materials + TutorMatch V1

**Goal:** Complete ClassHub feature set + seed TutorMatch supply quietly

### Sprint 7 (Week 13–14): Course Materials Library

**Deliverables:**
- [ ] `ClassMaterial` (classId, sessionId nullable, title, fileUrl, fileType, uploadedAt)
- [ ] Upload: PDF, DOCX, PPTX, images, YouTube embed links
- [ ] Organize by session or standalone
- [ ] Student/parent access via join code or parent portal token
- [ ] Lock materials behind paid enrollment
- [ ] Download count tracking

**Reuse:** Existing media upload system + S3/MinIO (already built)

---

### Sprint 8 (Week 15–16): TutorMatch V1 — Public Profile + Inquiry

**This is NOT the full marketplace yet. Goal: seed supply, get profiles live.**

**Deliverables:**
- [ ] `TutorProfile` (userId, subjects[], areas[], priceMin, priceMax, bio, sessionTypes[], videoUrl, isPublished)
- [ ] `TutorCredential` (profileId, type, fileUrl, verifiedAt)
- [ ] Profile creation from existing teacher's ClassHub account (one-click)
- [ ] Public profile: `/tutors/[slug]` — no auth for viewing
- [ ] **Information gating:** name/photo/subjects/price free; phone/exact address locked behind booking
- [ ] Simple inquiry form (name, phone, message) → teacher gets notification
- [ ] Admin verification queue (reuse existing admin console pattern)
- [ ] NO search/boost/booking yet — just profiles + inquiry

---

## Month 5 — Search + Zalo OA + Launch Prep

**Goal:** TutorMatch searchable + Zalo notifications live + ready for public

### Sprint 9 (Week 17–18): TutorMatch Search + Zalo OA

**TutorMatch search:**
- [ ] `/tutors` search page with filters: subject, district, price range, session type (online/offline/hybrid)
- [ ] PostgreSQL full-text search (sufficient for <5,000 profiles)
- [ ] Sort: newest, highest rated (once reviews exist), most inquiries
- [ ] "Accepting students" status toggle (teacher controls)

**Zalo OA integration (critical — 80% open rate vs 20% email):**
- [ ] Zalo Official Account API integration
- [ ] `ZaloNotificationChannel` implementing `INotificationChannel`
- [ ] Parent opt-in: receive notifications via Zalo instead of/in addition to email
- [ ] Notification types: attendance report, tuition reminder, session reminder
- [ ] Fallback to email if parent hasn't connected Zalo

---

### Sprint 10 (Week 19–20): Launch Readiness

**Deliverables:**
- [ ] Onboarding flow: sign up → create landing page → create first class (5-minute time-to-value)
- [ ] In-app onboarding checklist widget (progress bar)
- [ ] Annual plan payment flow
- [ ] Referral system: unique link, 1 month free for successful referral
- [ ] Help docs (Notion embeds or basic in-app FAQ)
- [ ] Pricing page
- [ ] Load testing: 1,000 concurrent users acceptable
- [ ] Security audit: rate limits, tenant isolation, webhook signatures (already done)

---

## TutorMatch — Staged Opening Plan

```
Month 5 (Sprint 8): Profiles live, inquiry only
  → NO search, NO booking, NO boost
  → Seed supply: invite ClassHub teachers to create profiles

Month 6 (Launch): Searchable directory opens
  → Filter by subject, area, price, session type
  → Parents can find tutors, send inquiry
  → NO booking fee, NO boost yet

Month 8–9: Booking + 30k deposit
  → Only when 500+ parent searches/month confirmed
  → Booking fee 30k (trust mechanism)
  → Reviews after session completion

Month 10–12: Boost rankings
  → Only when traffic is real + can show teacher click/view data
  → Sell boost at 200k–300k/month
  → ROI pitch: "3 new inquiries last month from your profile"
```

---

## Time Allocation — Solo Founder Weekly

```
Mon–Wed: Feature development (sprint current week)
Thu:     User calls / feedback sessions with beta users (3 minimum)
Fri AM:  Content creation (TikTok / Facebook about building + teacher tips)
Fri PM:  Sprint review, plan next week
Weekend: Rest OR respond in beta Zalo group
```

**Marketing in parallel (not after building):**
```
Month 1: "Xây app cho giáo viên 1 mình" — document the build
Month 2: Show first beta teacher using it — before/after story
Month 3: Demo anonymous board → viral in teacher communities
Month 4: TutorMatch teaser — "Tìm gia sư không qua môi giới"
Month 5: Waitlist → collect 200 emails + 50 Zalo contacts
```

---

## Launch Checklist (Before Month 6)

```
Product:
□ 10 beta teachers with landing page live
□ 5 teachers collecting tuition online (real money flowing)
□ At least 20 parent portal activations
□ Zalo OA sending notifications (not just email)
□ 30+ tutor profiles on TutorMatch
□ TutorMatch searchable
□ Zero critical bugs in payment flow
□ Mobile responsive on iPhone + Android (online teacher use case!)

Business:
□ Công ty TNHH registered
□ PayOS merchant account (KLB free tier active)
□ VNPay QR backup ready
□ Zalo OA officially approved
□ Privacy policy + ToS live
□ Support Zalo account active

Marketing:
□ Landing page (klassi.vn) live
□ 200+ emails on waitlist
□ 5 testimonials from beta teachers
□ Pricing page published
□ 1 case study: "Thầy X chốt học sinh nhanh hơn thế nào"
```

---

## Sprint Velocity Estimate (AI-assisted)

| Task type | Time |
|-----------|------|
| New domain entity + migration | 1–2 hours |
| Command handler | 1–2 hours |
| Query handler | 30 min – 1 hour |
| REST API controller | 30 min |
| Frontend list page (React Query) | 2–3 hours |
| Frontend form page | 2–4 hours |
| Email template | 30 min |
| **Full feature (CRUD + frontend)** | **1–2 days** |

At 3–4 features/week (half day marketing): 5 months = 50–60 features.  
MVP + Phase 1 requires ~42 features. **Timeline is achievable.**
