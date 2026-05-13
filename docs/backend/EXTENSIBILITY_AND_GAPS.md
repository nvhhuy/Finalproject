# Extensibility and intentional gaps

This document is for **forking the repo per customer** or **greenfield projects** built on the same skeleton. It lists what is already solid, what is deliberately minimal, and what you typically add when requirements appear.

## What is already in good shape

- **Layering**: Domains → Application (CQRS-style contracts) → Infrastructure → Api/Worker keeps business rules out of HTTP and persistence details behind interfaces.
- **Auth**: JWT + refresh pattern, Google id-token path, password hashing, role policy hook (`AdminOnly`).
- **Persistence**: EF Core, read/write DbContext split, interceptors (auditing, domain events), transactional action filter for command-style endpoints.
- **Integrations**: Pluggable null/real implementations (email, messaging, video, storage) so CI and first deploy work without every vendor.
- **Caching**: Redis or in-memory with repository cache base patterns.
- **Ops-friendly defaults**: forwarded headers, HSTS in non-dev, security headers, rate limiting on auth policy, **`/health`** with DB check, **`X-Correlation-Id`** on requests/responses and in **`ApiMeta`**.
- **JWT startup validation**: `JwtSettings.EnsureValidForSigning` enforces non-empty issuer/audience and **secret length ≥ 32** for HS256.
- **Structured logging**: `Observability` + Serilog (Console; optional OpenSearch in Development) for Api and Worker; request logging enriches **CorrelationId** and user name.
- **Cron-ready Worker**: `ICronJob` + `CronJobSchedulerHostedService` (Cronos); expressions configurable under **`Worker`** in appsettings.
- **Realtime placeholder**: `Realtime:Enabled` + optional Redis SignalR backplane; JWT via **`?access_token=`** on `/hubs/notifications`; see `src/Backend/Api/Configuration/RealtimeOptions.cs`.

## Database read/write vs containers

You do **not** need two Postgres servers for local development. Use **one** Postgres container and set **both** connection strings to the same host/database. The **read** DbContext exists so production can target a **replica** (different host) without refactoring queries.

## Gaps you should plan for per customer

| Topic | Current state | Typical next step |
|--------|----------------|-------------------|
| **Migrations in production** | Dev auto-migrates in `Program.cs` | Remove auto-migrate for prod; run `dotnet ef database update` or image entrypoint in CI/CD. |
| **Secrets** | `appsettings` + env vars | Azure Key Vault, AWS Secrets Manager, or sealed k8s secrets; never ship real secrets in git. |
| **Observability** | Serilog + OpenSearch in `local/docker-compose` (dev) | Add OpenTelemetry exporters; wire production log/metric backends; tune ISM retention per compliance. |
| **Tests** | Not part of base | Api integration tests (WebApplicationFactory), Application unit tests on handlers, contract tests for external APIs. |
| **API versioning** | Single version | `Asp.Versioning.Mvc` or path-based `/v1` when public API stability matters. |
| **Multi-tenant** | Single-tenant assumptions | Tenant resolution middleware, row-level `TenantId`, or schema-per-tenant; touches almost all queries. |
| **CORS** | Not pre-wired for REST | `AddCors` with explicit origins when SPA is on another host; **required** for browser SignalR if the client is not same-origin. |
| **File upload limits / antiforgery** | Defaults | Configure `FormOptions`, Kestrel limits, virus scan pipeline if users upload files. |
| **Email / SMS / push** | `NullEmailSender` | Swap for SendGrid, SES, Twilio per project. |
| **Packages** | `dotnet list package --vulnerable` | Bump AutoMapper, ImageSharp, System.Security.Cryptography.Xml when patched versions exist. |
| **EF design-time** | Migrations run via Api + DI | Optional `IDesignTimeDbContextFactory` if you want migrations without full host (needs simple DbContext factory without app-only interceptors). |

## How to extend without fighting the template

1. **New bounded context**: Under `src/Backend/`, add `Application/Contracts/Modules/<Feature>/`, `Application/<Feature>/`, `Infrastructure/Modules/<Feature>/`, register in `Infrastructure/DependencyInjection.cs` (or split into `Add<Module>Persistence` extension methods when the file grows).
2. **New policies**: Add constants in `src/Backend/Api/Authorization/AuthorizationPolicies.cs`, register in `src/Backend/Api/DependencyInjection.cs` `AddAuthorization`, use `[Authorize(Policy = ...)]`.
3. **New cross-cutting filter**: Api-only → `src/Backend/Api/Filters`; persistence-wide → interceptors or MediatR behaviors in Application.
4. **Customer-specific config**: Prefer `appsettings.{Customer}.json` + env `ASPNETCORE_ENVIRONMENT` or single `appsettings.Production.json` fed by deployment platform, not long-lived secrets in repo.

## Summary

The base is meant to be **boring and correct** for a first production deploy, not feature-complete. Treat this file as a **checklist**: copy it into the customer wiki and tick rows as you harden each engagement.
