# SaaS roadmap — tenant, plans, billing & credits

Product direction for turning this monorepo into a **multi-tenant SaaS**. Read with [`../backend/EXTENSIBILITY_AND_GAPS.md`](../backend/EXTENSIBILITY_AND_GAPS.md) and [`../deploy/SCALING.md`](../deploy/SCALING.md).

**Current base:** single-tenant data model; `TenantId` only appears in feature-flag rollout seeding. No `Tenant`, `Subscription`, or `Wallet` entities yet.

---

## Do you need both Follow 1 and Follow 2?

| Model | Typical use | Needed for “good SaaS”? |
|-------|-------------|-------------------------|
| **Follow 1 — Subscription plans** | B2B SaaS, per-org pricing, limits (users, storage, modules) | **Yes** for most B2B products |
| **Follow 2 — Credit wallet** | Usage-based, API credits, pay-per-feature, marketplace | **Only if** pricing is consumption-based or hybrid |

**Recommendation:** design **one entitlement layer** that both models plug into. Many successful SaaS products use **subscription + overage credits** (hybrid), not two separate apps.

You do **not** need both on day one. Ship **Follow 1 first** (tenant + plan + limits + checkout), add **wallet** when you have metered features (AI tokens, SMS, exports, etc.).

---

## Target experience (your two follows)

### Follow 1 — Plans per tenant

```text
Marketing site (global)
  /, /pricing, /about, /contact
       │
       ▼ checkout (Stripe / VNPay / manual invoice)
       │
       ▼ provision Tenant + Subscription
       │
Tenant public site
  /t/{slug}     or   {slug}.app.example.com
  (tenant-branded landing: about, contact, optional CMS)
       │
       ▼ sign-in / invite
       │
Tenant app (authenticated)
  Features gated by Plan + Usage limits
```

**Per plan (catalog):** max users, max storage, enabled modules (support, media, notifications, …), support tier, trial days.

**After purchase:** create `Tenant`, assign `Subscription`, create owner `User`, send onboarding email, activate slug/path.

### Follow 2 — Personal credit wallet (per user)

```text
User (global account)
  PersonalWallet — independent of tenant
       │
       ▼ top-up (payment provider webhook)
       │
       ▼ debit for metered actions on main app (no tenant context)
       │
Ledger (immutable): credit / debit / hold / refund
```

**Product rules (confirmed):**

- Buying a **subscription plan** creates a **tenant**; buyer becomes **owner**.
- **Credits** are **per user**, not per tenant — used for pay-per-task on the **main app**.
- Credits do **not** substitute for subscription and do **not** apply inside tenant routes (MVP).

---

## Unified architecture (support both follows)

```text
                    ┌─────────────────────┐
                    │   Plan catalog      │
                    │ (global, marketing) │
                    └──────────┬──────────┘
                               │
         ┌─────────────────────┼─────────────────────┐
         ▼                     ▼                     ▼
  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
  │ Subscription │    │    Wallet    │    │   Entitlement │
  │   (Follow 1) │    │  (Follow 2)  │    │    resolver   │
  └──────┬───────┘    └──────┬───────┘    └───────┬───────┘
         │                   │                    │
         └───────────────────┴────────────────────┘
                             │
                    ITenantContext + limits
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
           Users         Media quota    Feature flags
           Support        API rate       Modules on/off
```

**Entitlement resolver** (single place):

```csharp
// Conceptual — implement in Application layer
Task<EntitlementResult> ResolveAsync(TenantId tenant, FeatureKey feature, CancellationToken ct);
// Checks: active subscription? plan includes feature? under quota? wallet balance if metered?
```

Handlers and API filters call this instead of hard-coding plan logic.

---

## Domain model (minimum tables)

### Tenancy

| Entity | Purpose |
|--------|---------|
| `Tenant` | Org/customer: `Id`, `Slug`, `DisplayName`, `Status`, `CreatedAt` |
| `TenantMember` | `UserId`, `TenantId`, `Role` (Owner, Admin, Member) |
| `TenantDomain` | Optional custom domain → tenant mapping |
| `TenantBranding` | Logo, colors, public landing copy (JSON or columns) |

### Billing — Follow 1

| Entity | Purpose |
|--------|---------|
| `Plan` | Catalog: code, name, price, interval, trial days, **limits JSON** |
| `PlanFeature` | Or embed in limits: `max_users`, `max_storage_bytes`, `modules[]` |
| `Subscription` | `TenantId`, `PlanId`, status, period start/end, cancel at |
| `SubscriptionEvent` | Audit: created, renewed, failed, canceled |
| `PaymentProviderCustomer` | Stripe customer id, etc. |

### Billing — Follow 2

| Entity | Purpose |
|--------|---------|
| `Wallet` | `TenantId` (or `UserId`), `Currency`, `Balance` |
| `WalletLedgerEntry` | Append-only: amount, type, reference, idempotency key |
| `CreditPackage` | Top-up SKUs: 100k VND → 1000 credits |
| `MeteredSku` | Optional: price per action in credits |

### Cross-cutting

| Entity | Purpose |
|--------|---------|
| `UsageCounter` | `TenantId`, `MetricKey`, `Period`, `Value` (users count, storage bytes) |
| `CheckoutSession` | Pending signup before tenant exists |

**Limits example (`Plan.LimitsJson`):**

```json
{
  "maxUsers": 10,
  "maxStorageBytes": 10737418240,
  "modules": ["support", "media", "notifications"],
  "maxSupportTicketsOpen": 50,
  "apiRequestsPerMinute": 120
}
```

---

## Tenant resolution & routing

| Strategy | URL | Pros | Cons |
|----------|-----|------|------|
| **Path** | `app.com/t/acme/...` | One cert, simple Compose | Longer URLs |
| **Subdomain** | `acme.app.com` | Clean branding | Wildcard TLS, Traefik Host rules |
| **Custom domain** | `portal.acme.com` | Enterprise | DNS + ACME per customer |

**Suggested default:** path `/t/{slug}` for MVP; add subdomain in Traefik when stable.

**Backend:**

1. Middleware: resolve `TenantId` from host or path segment.
2. `ITenantContext` scoped per request.
3. EF global query filter: `.HasQueryFilter(e => e.TenantId == _tenantContext.Id)`.
4. JWT claims: `tenant_id`, `tenant_role` (user may belong to multiple tenants later).

**Frontend:**

- **Marketing app** (or routes in Next): `/`, `/pricing`, `/about`, `/contact` — no tenant context.
- **Tenant public**: `/t/[slug]` — static/ISR landing from `TenantBranding`.
- **Tenant app**: `/t/[slug]/app/...` or subdomain equivalent — authenticated, entitlement-aware UI.

**Admin console:** platform super-admin (all tenants) vs tenant-admin (one tenant) — two policy dimensions.

---

## Plan limits — features to gate

Map limits to existing modules in this repo:

| Limit key | Enforced at | Existing module |
|-----------|-------------|-----------------|
| `maxUsers` | Invite / register | Users |
| `maxStorageBytes` | Upload | Media |
| `modules.support` | Route + API policy | Support tickets |
| `modules.media` | Upload endpoints | Media |
| `modules.notifications` | SignalR / API | Notifications |
| `maxOpenSupportTickets` | Create ticket command | Support |
| `adminSeats` | Tenant admin role count | Admin membership |
| `impersonation` | Plan flag | Admin impersonation |
| `backupRestore` | Plan flag | Settings backup jobs |

Enforcement layers:

1. **API** — MediatR behavior or authorization handler: `RequireEntitlement("support")`.
2. **Application** — command validators read `IEntitlementService`.
3. **Worker** — skip jobs for suspended tenants.
4. **UI** — hide nav items; show upgrade CTA.

---

## Payments (provider-agnostic)

| Region / stage | Option |
|----------------|--------|
| Global | Stripe Checkout + Customer Portal + webhooks |
| Vietnam | VNPay, MoMo, PayOS — webhook → same internal `PaymentSucceeded` event |
| Enterprise | Manual invoice → admin activates subscription |

**Internal events (do not tie domain to Stripe):**

- `checkout.completed` → create tenant + subscription
- `subscription.renewed` / `failed` / `canceled`
- `wallet.topup.completed` → ledger credit

Store raw webhook payloads in `PaymentWebhookInbox` for idempotent replay.

---

## Phased implementation plan

### Phase 0 — Foundation (block everything else)

**Goal:** safe multi-tenant data plane.

- [ ] `Tenant`, `TenantMember`, `ITenantContext`, resolution middleware
- [ ] Add `TenantId` to core tables: `User` (membership not duplicate users), `MediaAsset`, `SupportTicket`, `Notification`, …
- [ ] EF global filters + integration tests proving **tenant A cannot read tenant B**
- [ ] Platform admin vs tenant-admin policies
- [ ] Docs: tenant onboarding for developers

**Estimate:** large; touches most queries. Do before billing.

### Phase 1 — Follow 1 MVP (subscription SaaS)

**Goal:** sell plans, provision tenant, enforce limits.

- [ ] `Plan`, `Subscription`, `UsageCounter` domain + admin CRUD (platform)
- [ ] Marketing pages: pricing, about, contact (Frontend)
- [ ] Checkout flow (Stripe test mode or manual “Activate” in admin for dev)
- [ ] Post-checkout provisioning job: Tenant + Owner + Subscription active
- [ ] Tenant public landing `/t/{slug}` (minimal branding)
- [ ] `IEntitlementService` + enforce `maxUsers`, `modules.*`
- [ ] Subscription states: `trialing`, `active`, `past_due`, `canceled` (read-only mode)

### Phase 2 — Hardening billing

- [ ] Webhook idempotency, dunning emails, grace period
- [ ] Customer portal link (change plan, cancel)
- [ ] Usage dashboard for tenant admin (users count, storage)
- [ ] Subdomain routing + optional custom domain

### Phase 3 — Follow 2 (credits)

**Goal:** top-up and spend credits.

- [ ] `Wallet` + `WalletLedgerEntry` (append-only, no negative balance without overdraft flag)
- [ ] Top-up checkout → credit ledger
- [ ] Metered SKUs debiting wallet (e.g. export, AI call)
- [ ] Hybrid: plan includes monthly credit grant (`subscription.credits_per_month`)

### Phase 4 — Scale & enterprise

- [ ] Per-tenant rate limits
- [ ] SSO per tenant (optional)
- [ ] Audit export, SLA plans
- [ ] See [`../deploy/SCALING.md`](../deploy/SCALING.md)

---

## What the current base already gives you

| Area | Ready to reuse |
|------|----------------|
| Auth JWT + roles | Extend claims with `tenant_id` |
| Feature flags | Per-tenant rollout (`TenantId` in context) |
| App settings | Pattern for tenant-scoped settings table |
| Outbox + Worker | Provisioning, webhooks, usage aggregation jobs |
| Media byte size / quota hooks | Comments already mention billing/quota |
| Admin console | Platform ops UI; add Plans/Tenants screens |
| Deploy / scale | [`SCALING.md`](../deploy/SCALING.md) |

---

## What a “good SaaS base” must have (checklist)

| Must have | Follow 1 | Follow 2 | This repo today |
|-----------|----------|----------|-----------------|
| Tenant isolation | ✅ | ✅ | ❌ Phase 0 |
| Plan / entitlement model | ✅ | optional | ❌ |
| Subscription lifecycle | ✅ | optional | ❌ |
| Payment webhooks | ✅ | for top-up | ❌ |
| Usage metering | ✅ | ✅ | partial (media bytes) |
| Public marketing site | ✅ | ✅ | ❌ (app only) |
| Tenant-branded entry | ✅ | optional | ❌ |
| Wallet / ledger | optional | ✅ | ❌ |
| Platform admin | ✅ | ✅ | ✅ (single-tenant admin) |

**Verdict:** Your two follows are **correct and standard**. A strong B2B SaaS usually **starts with Follow 1**; add Follow 2 when you sell metered add-ons or prepaid usage. Building both before Phase 0 (tenant isolation) creates billing on unsafe data — **do Phase 0 first**.

---

## Suggested module layout in repo

```text
src/Backend/
  Domains/Modules/Tenancy/
  Domains/Modules/Billing/          # Plan, Subscription, Wallet, Ledger
  Application/Modules/Tenancy/
  Application/Modules/Billing/
  Infrastructure/Modules/Tenancy/
  Infrastructure/Modules/Billing/
  Api/Controllers/Platform/         # Plans, tenants (super-admin)
  Api/Controllers/Tenant/           # Tenant-scoped app APIs
src/Frontend/
  app/(marketing)/                  # pricing, about, contact
  app/t/[slug]/                     # public tenant landing
  app/t/[slug]/(app)/               # authenticated tenant app
src/Admin/
  features/platform/                # tenants, plans, subscriptions
  features/tenant-billing/          # usage, upgrade (tenant admin)
```

---

## Open decisions (answer before Phase 1)

1. **One user, many tenants?** (consultant in multiple orgs) → `TenantMember` many-to-many; JWT carries active tenant.
2. **Pricing currency:** VND only vs multi-currency wallets.
3. **Trial without card?** → `Subscription.Status = Trialing`, hard limits, upgrade wall.
4. **Slug rules:** reserved words, uniqueness, rename policy.
5. **Canceled tenant:** soft-delete + read-only retention vs export-and-purge.

---

## Related documents

- [`../backend/EXTENSIBILITY_AND_GAPS.md`](../backend/EXTENSIBILITY_AND_GAPS.md) — multi-tenant gap
- [`../backend/PRODUCTION_READINESS_REPORT.md`](../backend/PRODUCTION_READINESS_REPORT.md) — scale limits
- [`../deploy/SCALING.md`](../deploy/SCALING.md) — infra when tenants grow
- [`../admin/FEATURE_CATALOG.md`](../admin/FEATURE_CATALOG.md) — future org switcher note
