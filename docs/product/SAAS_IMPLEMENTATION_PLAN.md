# SaaS implementation plan (production)

Master checklist for multi-tenant SaaS with **two commercial tracks**:

| Track | Who pays | Outcome |
|-------|----------|---------|
| **Follow 1 — Subscription** | User buys a plan | Becomes **tenant owner**, org limits by plan |
| **Follow 2 — Personal credits** | User tops up wallet | Pays **metered tasks on main app**, **no tenant** |

Tracks are **independent**. Same `User` may own a tenant **and** have a personal wallet.

**Related:** [`SAAS_ROADMAP.md`](./SAAS_ROADMAP.md) · [`../deploy/SCALING.md`](../deploy/SCALING.md)

---

## Terminology — what “MVP” means here

| Term | Meaning in this doc |
|------|---------------------|
| **MVP** | *Minimum Viable Product* for that phase: core flows work end-to-end (API + UI + tests where listed). **Not** “demo only / cannot deploy.” |
| **✅ Done** | Phase checklist complete for the scope described. |
| **✅ Done (MVP)** | Same as Done, but known gaps are listed in [Upgrade backlog](#upgrade-backlog-do-later) — ship core now, polish later. |
| **⏳ Deferred** | Explicitly out of scope until a later upgrade. |
| **Base / foundation** | Usually **Phase 0 only** (tenancy). Phases 1–4 are **core SaaS**, not “base only.” |

**Deploy note:** Before any environment, run `./tools/backend/migrate.sh` (includes `AddTenantSsoAndNotificationDelivery` and prior billing/tenancy migrations).

---

## Production readiness (snapshot)

Use this table to decide *what you can ship now* vs *what to upgrade next*. Update when backlog items ship.

| Tier | Status | You can… | Not included yet (upgrade later) |
|------|--------|----------|----------------------------------|
| **A — Core SaaS (deployable)** | ✅ Current | Run multi-tenant app in staging/prod: tenants, plans, subscriptions, entitlements, webhooks (Stripe/VNPay/PayOS inbox), wallet, invoices, subdomain/custom domain (manual DNS), rate limits, dunning, admin audit export | Self-serve card checkout UI; full payment UX for wallet top-up |
| **B — Commercial polish** | ⏳ Backlog | — | Stripe Checkout session + redirect from `/pricing`; wallet top-up via payment provider (not manual API); pricing CTA wired to hosted checkout |
| **C — Enterprise / ops** | ⏳ Backlog | — | Tenant OIDC **login UI** on `/t/[slug]` (API exists); encrypt SSO client secrets at rest; OpenSearch per-tenant index (4.4); wildcard TLS + automated DNS for custom domains |

**How to read the status tracker below:** Phases **0–4 = Tier A (core)**. Items marked MVP or “manual / pending / optional / deferred” in phase tables are **Tier B or C** work — track them in the upgrade backlog, not as missing Phase 0.

---

## Upgrade backlog (do later)

Prioritized when moving from **Tier A → B → C**. Check off here; no need to reopen closed phase tables unless acceptance criteria change.

| Priority | ID | Item | Tier | Depends on | Notes |
|----------|-----|------|------|------------|-------|
| P0 | U.0 | Apply DB migrations + smoke (e2e / integration with Postgres) | A | — | `./tools/backend/migrate.sh`; verify Admin audit export, tenant SSO settings |
| P1 | U.1 | Stripe Checkout UI (hosted session → existing webhook provision) | B | U.0 | Replaces manual `POST /billing/subscribe` for most users |
| P2 | U.2 | Wallet top-up via payment provider (not manual `POST /wallet/top-up`) | B | U.1 | Same webhook/inbox pattern as subscription |
| P3 | U.3 | Public tenant OIDC sign-in UI (`/t/[slug]` or subdomain) | C | — | Backend: `POST /auth/tenant-sso/{slug}` + `GET /public/tenants/{slug}/sso` |
| P4 | U.4 | Encrypt `TenantSsoSettings.OidcClientSecret` at rest | C | U.3 | MVP stores plain text |
| P5 | U.5 | OpenSearch per-tenant index (4.4) | C | — | Serilog sink exists; index routing TBD |
| P6 | U.6 | Production TLS wildcard + custom-domain DNS automation | C | — | Phase 3.7 has TXT verify + Traefik example; automation optional |
| — | U.7 | Credits inside tenant app routes | Product | — | **Intentionally excluded** in Phase 2 MVP; reopen only if product changes |

---

## Architecture principles

1. **User is global** — one account, global email uniqueness; `TenantMember` links user ↔ tenant.
2. **Tenant-scoped data** — `SupportTicket`, `MediaAsset`, tenant `AppSetting` carry `TenantId` + EF global query filters.
3. **Entitlement resolver** — single service; subscription limits and credit debits both call it (later phases).
4. **Payment provider agnostic** — webhooks → internal domain events; no Stripe types in domain.
5. **Platform admin vs tenant admin** — `UserRoles.Admin` = platform; `TenantMemberRole` = org.

---

## Phase 0 — Tenancy foundation ✅

**Goal:** Safe multi-tenant data plane before billing.

### Backend

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 0.1 | `Tenant`, `TenantMember` aggregates | Slug unique, owner, status lifecycle |
| 0.2 | `ITenantContext` + middleware | Resolve `X-Tenant-Id` / JWT claim; validate membership |
| 0.3 | EF global query filters | Tenant-scoped entities isolated; platform admin bypass |
| 0.4 | `TenantId` on `SupportTicket`, `MediaAsset` | Set on create; migration backfills default tenant |
| 0.5 | Platform API | Admin: create/list tenants; Public: GET by slug |
| 0.6 | Integration tests | Tenant A cannot read tenant B data |

### Not in Phase 0

- Billing, wallet, marketing pages, `/t/{slug}` frontend routes (Phase 1+).

---

## Phase 1 — Subscription SaaS (Follow 1)

**Goal:** Sell plans → provision tenant → enforce limits.

### 1A — Plan catalog & entitlements

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 1.1 | `Plan` entity + limits JSON | `maxUsers`, `maxStorageBytes`, `modules[]`, trial days |
| 1.2 | `IEntitlementService` | `CanUseFeature`, `GetLimit`, `GetUsage` |
| 1.3 | `UsageCounter` | Periodic aggregation: users, storage bytes |
| 1.4 | Platform admin UI | CRUD plans API ✅; Admin `/plans` list ✅ |
| 1.5 | Enforcement | MediatR behavior + API policies on gated modules |

**Suggested limit keys:**

```json
{
  "maxUsers": 10,
  "maxStorageBytes": 10737418240,
  "modules": ["support", "media", "notifications"],
  "maxOpenSupportTickets": 50,
  "adminSeats": 3,
  "impersonation": false,
  "backupRestore": false
}
```

### 1B — Subscription lifecycle

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 1.6 | `Subscription` entity | `trialing`, `active`, `past_due`, `canceled` |
| 1.7 | Checkout (Stripe test / manual activate) | `POST /billing/subscribe` MVP ✅; Stripe pending |
| 1.8 | Provisioning job | Idempotent: tenant + subscription + welcome email |
| 1.9 | Grace / read-only | `past_due` → read-only tenant app |
| 1.10 | Webhook inbox | ✅ inbox + signature verify + `checkout.session.completed` → provision |

### 1C — Marketing & tenant public site

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 1.11 | Global marketing routes | `/`, `/pricing`, `/about`, `/contact` ✅ |
| 1.12 | Pricing page | Plans from API, CTA → checkout ✅ |
| 1.13 | Tenant public landing | `/t/[slug]` ✅ |
| 1.14 | Tenant app shell | `/t/[slug]/app` sets tenant context → `/app` ✅ (full isolated shell later) |
| 1.15 | Invite members | `POST /tenants/{id}/members` + `maxUsers` check ✅ |

### 1D — Tests & ops

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 1.16 | Integration tests | ✅ checkout, invite, member limit, webhook provision |
| 1.17 | E2e | ✅ pricing + subscribe smoke |
| 1.18 | Docs | ✅ FEATURE_CATALOG updated |

---

## Phase 2 — Personal wallet (Follow 2)

**Goal:** User tops up credits; pay per metered action on **main app** (no tenant).

### 2A — Wallet core

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 2.1 | `PersonalWallet` (1:1 User) | ✅ Created on first top-up |
| 2.2 | `WalletLedgerEntry` | ✅ Append-only ledger |
| 2.3 | Balance rules | ✅ No negative balance; idempotent debit |
| 2.4 | Top-up checkout | ✅ MVP `POST /wallet/top-up` (manual) |
| 2.5 | Wallet API | ✅ balance, ledger, actions |

### 2B — Metered actions

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 2.6 | `MeteredAction` catalog | ✅ `MeteredActions` static catalog |
| 2.7 | `DebitCreditsCommand` | ✅ `POST /wallet/debit` |
| 2.8 | Initial actions | ✅ export_report, premium_job, bulk_notification |
| 2.9 | UI | ✅ `/app/wallet` |

### 2C — Rules (per product decision)

- Credits **do not** fund tenant subscription.
- Credits **do not** apply inside tenant app routes (MVP).
- User may **both** own tenant (Follow 1) and hold personal wallet (Follow 2).

---

## Phase 3 — Billing hardening

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 3.1 | Dunning emails | ✅ Worker job + `IBillingDunningNotifier` (24h throttle) |
| 3.2 | Customer portal | ✅ cancel + change plan API + `/app/tenant` UI |
| 3.3 | Invoices / receipts | ✅ `BillingInvoice` + list APIs + `/app/tenant` & `/app/wallet` + Admin `/invoices` |
| 3.4 | VNPay / PayOS adapter | ✅ `POST /webhooks/vnpay` & `/webhooks/payos` → same inbox as Stripe |
| 3.5 | Usage dashboard | ✅ `GET .../entitlements` + `/app/tenant` |
| 3.6 | Subdomain routing | ✅ Traefik `HostRegexp` + Next middleware + `Tenancy:SubdomainBaseHost` |
| 3.7 | Custom domain | ✅ `TenantDomain` + DNS TXT verify + owner UI; Traefik file-provider example for ACME |

---

## Phase 4 — Scale & enterprise

| ID | Feature | Acceptance criteria |
|----|---------|---------------------|
| 4.1 | Per-tenant rate limits | ✅ `tenant-api` policy keyed by tenant + `PlanLimits.ApiRequestsPerMinute` |
| 4.2 | SSO per tenant | ✅ OIDC id_token sign-in + owner SSO settings API |
| 4.3 | Audit export | ✅ CSV export API + Admin download |
| 4.4 | OpenSearch per-tenant index | ⏳ Deferred (Serilog sink exists; per-tenant index TBD) |
| 4.5 | Notification delivery service | ✅ Retry job for undelivered live notifications |
| 4.6 | Hybrid credits | ✅ `PlanLimits.MonthlyCreditGrant` + `SubscriptionCreditGrantJob` |

See [`../deploy/SCALING.md`](../deploy/SCALING.md).

---

## Module layout (target)

```text
src/Backend/
  Domains/Modules/Tenancy/
  Domains/Modules/Billing/           # Phase 1–2
  Application/Modules/Tenancy/
  Application/Modules/Billing/
  Infrastructure/Modules/Tenancy/
  Infrastructure/Modules/Billing/
  Api/Controllers/Platform/
  Api/Controllers/Tenancy/
src/Frontend/
  app/(marketing)/                   # Phase 1C
  app/t/[slug]/                      # Phase 1C
  app/(main)/wallet/                 # Phase 2
src/Admin/
  features/platform/                 # tenants, plans
  features/billing/                  # subscriptions, webhooks
```

---

## Implementation order (strict)

```text
Phase 0 Tenancy ──► Phase 1A Plans ──► Phase 1B Subscription ──► Phase 1C Frontend
                                                      │
Phase 2 Wallet ◄──────────────────────────────────────┘ (parallel after 1A)
Phase 3 Hardening ──► Phase 4 Enterprise
```

**Never skip Phase 0** before selling plans or storing tenant data.

---

## Status tracker

**Current product tier:** **A — Core SaaS (deployable)**. See [Production readiness](#production-readiness-snapshot) and [Upgrade backlog](#upgrade-backlog-do-later).

| Phase | Status | Tier | Notes |
|-------|--------|------|-------|
| 0 | ✅ Done | A | Tenancy foundation — not the stopping point; phases 1–4 built on this |
| 1 | ✅ Done (MVP) | A (+ B gaps) | Webhook provision, entitlements, e2e; **B:** Stripe Checkout UI → U.1 |
| 2 | ✅ Done (MVP) | A (+ B gaps) | Wallet API + `/app/wallet`; manual top-up; **B:** provider top-up → U.2 |
| 3 | ✅ Done (MVP) | A (+ C gaps) | Subdomain + custom domain + invoices/webhooks; **C:** TLS/DNS automation → U.6 |
| 4 | ✅ Done (MVP) | A (+ C gaps) | Rate limits, hybrid credits, SSO API, audit export, notification retry; **C:** OpenSearch → U.5, SSO UI/secrets → U.3–U.4 |

Update this table when a **phase** completes. Move **Tier B/C** items to ✅ in the [Upgrade backlog](#upgrade-backlog-do-later) when shipped (do not rename phases).
