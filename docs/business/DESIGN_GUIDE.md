# Klassi — Design guide (UI v4)

**Last updated:** 2026-06-04  
**Applies to:** Frontend (Klassi brand), pitch deck, email · Admin uses **separate** operator palette

---

## Split: product vs operator console

| App | Brand colors | Rationale |
|-----|--------------|-----------|
| **Frontend** (`src/Frontend/`) | Duolingo-inspired green v4 | End-user / teacher / parent — mát, học tập, dễ nhìn |
| **Admin** (`src/Admin/`) | **Base project blue-on-slate** (unchanged) | Internal ops tool — neutral, không cần marketing brand |
| **Email** | Matches Frontend primary `#22C55E` | Cùng nhận diện với app người dùng |
| **Pitch deck** | Green brand + cool dark slides | Investor/marketing; aligned with Frontend |

---

## Frontend — Klassi brand (current, keep)

| Token | Light | Dark mode | Use |
|-------|-------|-----------|-----|
| `--color-primary` | `#22C55E` | `#4ADE80` | CTA, brand, nav active |
| `--color-primary-hover` | `#16A34A` | `#22C55E` | Hover |
| `--color-primary-soft` | `#DCFCE7` | `rgb(34 197 94 / 0.18)` | Chips, active nav |
| `--color-accent-foreground` | `#166534` | `#BBF7D0` | Text on soft green |
| `--color-background` | `#F7FEF9` | `#0F1A12` | Page background |
| `--color-foreground` | `#1A2E1A` | `#ECFDF5` | Body text |
| `--color-muted` | `#5C6B5C` | `#9CA89C` | Secondary text |
| `--color-success` | `#16A34A` | `#4ADE80` | Paid, present, OK |
| `--color-market` | `#1CB0F6` | `#38BDF8` | TutorMatch / marketplace accent |

**Logo (v4.1):** emerald tile + white **K** — `public/logo-mark.svg`, `public/logo.svg` (wordmark), `public/favicon.svg`; React: `components/brand/KlassiLogoMark.tsx`, `AppBrand`

**Files:** `src/Frontend/app/globals.css`, `lib/config/branding.ts`, `public/manifest.webmanifest`

---

## Admin — operator UI (reverted to original)

| Token | Value | Use |
|-------|-------|-----|
| `--color-surface` | `#0F1419` | Page background |
| `--color-surface-elevated` | `#1A2332` | Cards |
| `--color-accent` | `#3B82F6` | Buttons, links, focus |
| `--color-accent-hover` | `#2563EB` | Hover |
| `--color-success` | `#22C55E` | Success states |
| `--color-warning` | `#F59E0B` | Warnings |

**File:** `src/Admin/src/index.css` — do **not** mirror Frontend green unless product explicitly requests unified admin branding.

---

## Pitch & email

- Pitch: [`pitch/klassi-pitch-deck.html`](./pitch/klassi-pitch-deck.html) — green v4 + UI mock slide 3
- Email headers/buttons: `#22C55E` — `src/Backend/Infrastructure/Email/Templates/*.html`

---

## Color exploration log (for reference)

| Direction | Verdict |
|-----------|---------|
| Purple `#5B4AE8` | Startup/AI vibe; dropped for Klassi product |
| Warm orange `#E07A2F` | Sáng tạo nhưng **chói** trên dashboard — dropped |
| Lime `#58CC02` | EdTech nhưng dễ trông **vàng/cam** trên màn — đổi sang emerald |
| **Green `#22C55E` (v4.1)** | **Selected for Frontend** — xanh lá rõ, mát, không lime |

---

## Do / don't

**Do**

- Frontend: green primary, darker green for money rows, sky blue for TutorMatch only
- Admin: keep slate + blue for operator density and familiarity

**Don't**

- Force Admin to match Frontend green (ops UI ≠ marketing brand)
- Use orange/purple as Frontend primary without user testing
