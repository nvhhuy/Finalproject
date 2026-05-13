# Architecture (DDD-aligned API boundary)

This backend follows **Microsoft’s practical DDD guidance** for .NET services: keep the **domain model** rich and encapsulated, expose a thin **API layer** with stable contracts, and use **CQRS-style** separation between reads and writes where it pays off ([DDD-oriented microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice), [eShop patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/eshoponcontainers-cqrs-ddd-microservice)).

See **`MODULE_STRUCTURE.md`** for per-domain folders (aligned with *dotnet-cleanarchitecture-ddd-cqrs-sample* and *FaceBookClone*-style `Modules` layouts). For **coding habits** mapped from those repos (handlers, DTOs, DI), see **`STYLE_FROM_REFERENCE_PROJECTS.md`**.

## Layers

| Layer | Responsibility |
|-------|------------------|
| **Domains** | Aggregates, entities, value objects, domain events (no infrastructure). |
| **Application** | Commands/queries, handlers, validation, orchestration; depends on abstractions (`Application/Contracts`). |
| **Infrastructure** | EF Core, cache, email, storage, external APIs, EF interceptors, app settings persistence, messaging adapters. |
| **Api** | HTTP, auth, filters, **API models** (`Api/Models`), mapping `Result` → standard **`ApiResponse<T>`**. |
| **Worker** | Background execution host for cron and queue-based consumers. |

The **API project must not own business rules**. It translates HTTP to MediatR messages and shapes responses.

## Standard HTTP contract: `ApiResponse<T>`

All normal JSON responses use:

- `success` — `true` or `false`
- `data` — payload when successful (nullable for empty success)
- `error` — `ApiErrorPayload` when failed (`code`, `message`, `httpStatus`, optional `validationErrors`, optional `extensions`)
- `meta` — `ApiMeta` (`traceId`, `timestampUtc`, optional `extensions`)

Clients and OpenAPI generators can rely on one envelope; extend **`meta.extensions`** or **`error.extensions`** without breaking older clients.

## Controllers: partial classes

Read and write actions for the same resource can live on **one controller type** split across **partial class files** (e.g. `AdminUsersController.Read.cs` / `AdminUsersController.Write.cs`). Shared behavior lives on **`ApiControllerBase`**.
Standalone resources (for example admin app settings) use dedicated controllers when partial split is unnecessary.

## REST shape (CQRS-friendly)

Prefer **resource + HTTP verb** instead of separate `/read` and `/write` URL segments:

- `GET api/admin/users` — query (read DB)
- `POST api/admin/users/{id}/lock` — command (write DB, transactional)
- `PUT api/admin/settings/allow-public-register` — runtime feature toggle for public registration

## When to add more DDD

Microsoft recommends **selective** DDD: invest in aggregates and domain events where rules are non-trivial. Simple CRUD can stay thin. This codebase includes **domain events** and **read/write repositories** as optional depth you can extend.
