# Feature Roadmap — Klassi

**Last updated:** 2026-06-02  
**Version:** 2.0 — Tool-first strategy  
**Notation:** ✅ = done (base codebase) | 🔨 = build Phase 1 | 📅 = Phase 2+ | ❌ = not planned

---

## Legend

| Phase | Timeline | Goal |
|-------|----------|------|
| **Phase 0** | Now | Infrastructure already exists in base codebase |
| **Phase 1 (MVP)** | Month 1–5 | Landing page + ClassHub core + TutorMatch V1 |
| **Phase 2 (PMF)** | Month 6–12 | Traction, validate monetization, TutorMatch open |
| **Phase 3 (Growth)** | Month 13–18 | Scale, automation, expansion |
| **Phase 4 (Scale)** | Month 19–24 | Multi-city, enterprise, platform features |
| **Phase 5 (Moat)** | Year 3+ | AI, content marketplace, defensibility |

---

## PLATFORM INFRASTRUCTURE (Phase 0 — Already Built)

| Feature | Status |
|---------|--------|
| User registration + email verification | ✅ |
| Google OAuth login | ✅ |
| 2FA / TOTP | ✅ |
| Multi-tenant architecture | ✅ |
| Subscription plans (Free/Starter/Pro) | ✅ |
| VNPay + PayOS payment + HMAC webhook | ✅ |
| Email delivery (SMTP + outbox) | ✅ |
| In-app notifications (SignalR realtime) | ✅ |
| File upload (S3/MinIO) | ✅ |
| Admin console (users, tenants, support) | ✅ |
| Wallet / balance system | ✅ |
| Rate limiting + idempotency | ✅ |
| CI/CD + Docker + Traefik | ✅ |
| i18n (vi/en/zh) | ✅ |

---

## TEACHER LANDING PAGE (Phase 1 — Priority #1)

**Entry point for the entire product. Build this first.**

| Feature | Phase | Priority |
|---------|-------|----------|
| 3 pre-built templates (minimal, modern, colorful) | Phase 1 | **P0** |
| Editable sections: hero, about, courses, testimonials, FAQ | Phase 1 | **P0** |
| CTA types: học thử, đăng ký, nhắn Zalo, giữ chỗ | Phase 1 | **P0** |
| Public URL: `klassi.vn/[slug]` | Phase 1 | **P0** |
| SEO metadata (title, description, OG) | Phase 1 | **P0** |
| Mobile-first responsive | Phase 1 | **P0** |
| Lead capture form → Inquiry record | Phase 1 | **P0** |
| Klassi branding badge (removable via add-on) | Phase 1 | P1 |
| Custom domain add-on (`tencuaban.vn`) | Phase 2 | P1 |
| Block enable/disable + reorder sections | Phase 2 | P1 |
| Sync with real reviews from ClassHub | Phase 2 | P1 |
| A/B testing for CTAs | Phase 4 | P3 |
| Drag-and-drop editor | Phase 4 | P3 — only if strong user demand |

---

## ClassHub — Class Management

| Feature | Phase | Priority |
|---------|-------|----------|
| Create/edit/archive classes | Phase 1 | **P0** |
| Session type: offline / online / hybrid | Phase 1 | **P0** |
| Online class: Zoom/Meet link per session | Phase 1 | **P0** |
| Recurring schedule | Phase 1 | **P0** |
| Add/remove students | Phase 1 | **P0** |
| Student profile (name, parent contacts, notes) | Phase 1 | **P0** |
| Class join code (anonymous board access) | Phase 1 | **P0** |
| Class capacity management | Phase 1 | P1 |
| Bulk import students (CSV) | Phase 2 | P1 |
| Multi-teacher class (co-teacher) | Phase 3 | P2 |
| Calendar view | Phase 2 | P1 |

---

## ClassHub — Attendance

| Feature | Phase | Priority |
|---------|-------|----------|
| Create session with type (offline/online/hybrid) | Phase 1 | **P0** |
| Mark attendance per student (Present/Absent/Late) | Phase 1 | **P0** |
| Teacher notes per session | Phase 1 | **P0** |
| Auto-email/Zalo parent after attendance saved | Phase 1 | **P0** |
| Monthly attendance summary report | Phase 1 | P1 |
| Make-up class tracking | Phase 2 | P1 |
| 3-absences-in-a-row alert | Phase 2 | P2 |
| QR code check-in | Phase 3 | P3 |

---

## ClassHub — Tuition & Payments (Core Paid Feature)

| Feature | Phase | Priority |
|---------|-------|----------|
| Manual tuition record per student | Phase 1 | **P0** |
| Auto-generate monthly tuition (Quartz job) | Phase 1 | **P0** |
| PayOS payment link per student | Phase 1 | **P0** |
| VNPay backup payment link | Phase 1 | **P0** |
| Auto D-3 reminder to parents | Phase 1 | **P0** |
| Auto D+1 overdue reminder | Phase 1 | **P0** |
| Debt dashboard (paid/unpaid at a glance) | Phase 1 | **P0** |
| Transaction fee deduction (tier-based) | Phase 1 | **P0** |
| Cash payment recording (manual) | Phase 1 | P1 |
| Teacher payout to bank | Phase 1 | P1 |
| Invoice PDF generation | Phase 2 | P1 |
| Partial payment support | Phase 2 | P2 |
| Semester fee (multi-month upfront) | Phase 3 | P2 |

---

## ClassHub — Parent Portal

| Feature | Phase | Priority |
|---------|-------|----------|
| Magic link (no account required) | Phase 1 | **P0** |
| Attendance history | Phase 1 | **P0** |
| Tuition status + pay online button | Phase 1 | **P0** |
| Upcoming schedule | Phase 1 | P1 |
| Session notes from teacher | Phase 1 | P1 |
| **Parent timeline** (full history: attendance + payments + notes) | Phase 1 | **P0** |
| Zalo notification opt-in | Phase 1 | **P0** |
| Course materials access | Phase 2 | P1 |
| Parent account (optional registration) | Phase 2 | P2 |
| Parent-teacher messaging | Phase 3 | P2 |

---

## ClassHub — Anonymous Q&A Board

**Differentiator vs all international competitors.**

| Feature | Phase | Priority |
|---------|-------|----------|
| Post anonymous question (no login) | Phase 1 | P1 |
| Teacher view: all questions, reply inline | Phase 1 | P1 |
| Answered questions visible to class | Phase 1 | P1 |
| Upvote system (IP-based) | Phase 1 | P1 |
| Question moderation (archive/pin) | Phase 1 | P1 |
| Rate limiting per IP (anti-spam) | Phase 1 | P1 |
| Anonymous peer discussion thread | Phase 4 | P3 |

---

## ClassHub — Course Materials

| Feature | Phase | Priority |
|---------|-------|----------|
| Upload files per class (PDF, PPTX, DOCX, images) | Phase 1 | P1 |
| YouTube embed links | Phase 1 | P1 |
| Organize by session or standalone | Phase 1 | P1 |
| Access via join code or parent portal | Phase 1 | P1 |
| Lock materials behind paid enrollment | Phase 2 | P1 |
| Download count tracking | Phase 2 | P2 |
| Quiz/homework assignments | Phase 4 | P3 |
| AI-generated quiz from materials | Phase 5 | P4 |

---

## ClassHub — Analytics & Reports

| Feature | Phase | Priority |
|---------|-------|----------|
| Monthly revenue summary | Phase 2 | P1 |
| Outstanding tuition report (CSV export) | Phase 2 | P1 |
| Attendance heat map | Phase 3 | P2 |
| Student retention per class | Phase 2 | P1 |
| Class growth over time | Phase 2 | P2 |
| Lead → trial → enrollment tracking | Phase 2 | **P0** |

---

## TutorMatch — Profiles (Phase 1, Month 4)

| Feature | Phase | Priority |
|---------|-------|----------|
| Profile creation from ClassHub account | Phase 1 | **P0** |
| Subjects, areas, price range | Phase 1 | **P0** |
| Session types: offline / online / hybrid | Phase 1 | **P0** |
| Intro video (upload or YouTube) | Phase 1 | P1 |
| Credential upload + admin verification | Phase 1 | **P0** |
| Verification badges (Identity, Credential) | Phase 1 | **P0** |
| "Accepting students" toggle | Phase 1 | **P0** |
| Public profile URL slug | Phase 1 | **P0** |
| Simple inquiry form (name, phone, message) | Phase 1 | **P0** |

**Information gating:**

| Info | Free (anyone) | Locked (requires booking) |
|------|---------------|--------------------------|
| Name, photo, subjects | ✅ | |
| Session types (online/offline) | ✅ | |
| Price range | ✅ | |
| Verified badge | ✅ | |
| Review summary | ✅ | |
| Full phone number | | Book trial (30k) |
| Exact address | | Book trial (30k) |

---

## TutorMatch — Search & Discovery (Phase 2 staged)

| Feature | Phase | Priority |
|---------|-------|----------|
| Searchable directory (Month 6) | Phase 2 | **P0** |
| Filter: subject, district, price, session type | Phase 2 | **P0** |
| Filter: online/offline/hybrid availability | Phase 2 | **P0** |
| Filter: verified status | Phase 2 | P1 |
| Sort: newest, highest rated, most inquiries | Phase 2 | P1 |
| Boosted profiles appear first (Month 10+) | Phase 2 | P1 |
| Saved/bookmarked tutors | Phase 3 | P2 |
| AI-powered recommendations | Phase 5 | P4 |

---

## TutorMatch — Booking & Reviews (Phase 2, Month 8+)

| Feature | Phase | Priority |
|---------|-------|----------|
| Book trial with 30k deposit (Month 8) | Phase 2 | **P0** |
| Teacher confirm/decline booking | Phase 2 | **P0** |
| Email + Zalo confirmation | Phase 2 | **P0** |
| Post-session review (1–5 + text) | Phase 2 | P1 |
| Session package purchase | Phase 3 | P1 |
| Recurring booking | Phase 3 | P2 |

---

## TutorMatch — Monetization (Phase 2, Month 10+)

| Feature | Phase | Priority |
|---------|-------|----------|
| Boost purchase UI | Phase 2 | **P0** (when traffic ready) |
| Boost analytics (impressions, clicks) | Phase 2 | P1 |
| Verification fee payment | Phase 2 | P1 |

---

## Platform Features

| Feature | Phase | Priority |
|---------|-------|----------|
| Mobile PWA (installable) | Phase 2 | P1 |
| Push notifications via PWA | Phase 2 | P1 |
| Zalo Mini App | Phase 3 | P2 |
| Annual subscription payment flow | Phase 1 | **P0** |
| Add-ons purchase flow (custom domain, etc.) | Phase 2 | P1 |
| iOS native app | Phase 4 | P2 |
| Android native app | Phase 4 | P2 |

---

## Admin Console

| Feature | Phase | Priority |
|---------|-------|----------|
| View/manage all tenants | ✅ Done | |
| View/manage all users | ✅ Done | |
| Support ticket management | ✅ Done | |
| Tutor profile verification queue | Phase 1 | **P0** |
| Approve/reject credentials | Phase 1 | **P0** |
| Platform revenue dashboard | Phase 2 | P1 |
| Transaction monitoring | Phase 2 | P1 |
| Boost management | Phase 2 | P1 |

---

## What NOT to Build in Phase 1

```
❌ Drag-and-drop landing page editor (overbuild, template-driven is enough)
❌ Full LMS / video hosting (not tutoring pain priority)
❌ Gamification (nice to have, not paying priority)
❌ AI lesson summary (Phase 5)
❌ Anonymous peer discussion (not core, Phase 4)
❌ Full TutorMatch with booking (Phase 2 — seed supply first)
❌ Native mobile app (PWA first, Phase 4 native)
```

---

## Phase-by-Phase Summary

| Phase | Timeline | Features | Goal |
|-------|----------|---------|------|
| **Phase 1 (MVP)** | Month 1–5 | Landing page + ClassHub full + TutorMatch V1 | 50 beta, 10 paying |
| **Phase 2 (PMF)** | Month 6–12 | TutorMatch search + booking + boost | 150 paying, first marketplace revenue |
| **Phase 3 (Growth)** | Month 13–18 | PWA, advanced analytics, Zalo Mini App | 400 paying, viral growth |
| **Phase 4 (Scale)** | Month 19–24 | Native apps, enterprise ClassHub, Hanoi | 1,000+ paying |
| **Phase 5 (Moat)** | Year 3+ | AI matching, content marketplace, score | Platform defensibility |
