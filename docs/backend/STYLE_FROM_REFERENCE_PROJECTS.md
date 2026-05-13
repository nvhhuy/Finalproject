# Conventions learned from reference projects

This document translates patterns from two local codebases into rules **this** solution already follows or should follow when you extend it:

| Reference | Path (on your machine) | What to copy |
|-----------|------------------------|----------------|
| **DDD + CQRS sample** (Books) | `~/Desktop/BaseDDDDotnet/dotnet-cleanarchitecture-ddd-cqrs-sample/src` | Vertical slice per use case, aggregate factories, thin `AddApplication`, `ValidateAsync` on commands. |
| **FaceBookClone** | `~/Desktop/DuyFacebookClone/FaceBookClone/Src/backend` | `Application/Modules/...`, contracts under `Application/Contracts/Modules/...`, API DTOs separate from commands, partial controllers, transaction filter on writes, logging in handlers. |

---

## 1. Folder layout (vertical slice)

**Books sample:** `Books.Application/Books/Commands/CreateBook/` contains `CreateBookCommand.cs`, `CreateBookCommandHandler.cs`, `CreateBookCommandValidator.cs` — **one folder = one use case**.

**This repo:** mirror that under the module root, e.g.

`Application/Modules/Users/Authentication/Commands/Register/` → `RegisterCommand`, `RegisterCommandHandler`, `RegisterCommandValidator`.

**Do:** add new commands as **new subfolders** next to existing ones, not loose files in a flat `Commands` folder.

---

## 2. Application layer responsibilities

| Concern | Books | FaceBookClone | This repo |
|---------|-------|---------------|-----------|
| Validation | `request.ValidateAsync(validator, ct)` → `ValidationError` | Often explicit `validator.ValidateAsync` in handler | Same as Books — **`Application.Extensions.ValidationExtensions`** (same shape as Books). |
| Business logic | Inside domain (`Book.CreateNew`, `AddAuthor`) | On entity + repository | Prefer **domain methods** on `User` etc.; handlers orchestrate. |
| Persistence | Repository + `SaveChangesAsync` in handler | Repository / unit of work | **Read/write repos** + `SaveChanges` in write path; optional **`[Transactional]`** on API for multi-step requests. |
| Logging | Less central in small handlers | **`ILogger<T>`** + warnings on failure | Add **`ILogger<Handler>`** when the flow is security-sensitive or hard to debug (optional, not required on every handler). |

---

## 3. Contracts (ports)

**FaceBookClone:** `Application.Contracts.Modules.User.IUserRespository` (example).

**This repo:** user persistence lives under **`Application.Contracts.Modules.Users.Persistence`** (and `Read` / `Write`).

**Do:** new domains get **`Application/Contracts/Modules/{Domain}/...`**; cross-cutting ports (cache, email, bus) stay under **`Application/Contracts/{Concern}/`**.

---

## 4. API layer

**FaceBookClone:**

- DTOs under **`API/Dtos/{Feature}/`**.
- Controllers map **DTO → command** in the action.
- **`BaseController`** helpers for consistent JSON (`ApiSuccess`, `Problem`).
- **`[Transaction]`** on mutating actions.

**This repo:**

- Controllers live under **`Api/Controllers/{Authentication|Users|Admin|Samples}/`** with namespaces `Api.Controllers.{Feature}`; shared base in **`Api/Controllers/Base/`**.
- DTOs under **`Api/Models/...`**.
- **`ApiControllerBase`** + **`ApiResponse<T>`** (standard envelope).
- **`[Transactional]`** + **`TransactionalActionFilter`** (same idea as FaceBook’s transaction filter).

**Do:** keep **commands/queries out of public API contracts** when you want a stable HTTP surface — add API request types and map to MediatR messages in the controller (auth register/login can be migrated gradually).

---

## 5. Dependency injection

**Books:** `AddApplication` only registers MediatR (minimal).

**This repo:** `AddApplication` registers MediatR, **pipeline behaviors**, AutoMapper — appropriate for a richer template.

**FaceBookClone:** API registers filters, Swagger, CORS in **`AddApiServices`**.

**Do:** keep **composition** split — `Program.cs` calls `AddApplication()` + `AddInfrastructure()`; API-only services stay in **`Api/Program.cs`** (or a future `Api/DependencyInjection.cs` if the file grows).

---

## 6. Domain layer

**Books:** `Books.Domain/BookAggregate/` with entities, value objects, events co-located.

**This repo:** **`Domains/Modules/Users/UserAggregate/`** with CLR namespace **`Domains.UserAggregate`** (stable for EF migrations).

**Do:** new aggregates under **`Domains/Modules/{Domain}/...`**; use **factory/static creators** (`User.CreateNew`, …) and **domain events** from the entity, not from the handler when possible.

---

## 7. Global usings (Application)

**Books:** `MediatR`, shared errors, `FluentValidation.Results`, etc.

**This repo:** `MediatR`, `Shared.Common`, `Shared.Errors`, `Application.Extensions`, plus **`FluentValidation`** for validators.

**Do:** add a **`global using`** only for types that appear in **most** handlers; avoid hiding important dependencies.

---

## 8. Checklist for a new feature

1. Pick module (e.g. **Users**) or create **`Modules/{NewDomain}`** (see **`MODULE_STRUCTURE.md`**).
2. Add **command/query folder** with handler + validator + message types.
3. Add or extend **contracts** in `Application/Contracts/Modules/...` if you need new ports.
4. Implement adapters in **`Infrastructure/Modules/...`** or shared `Infrastructure/...`.
5. Expose HTTP via **`Api/Controllers/{Feature}/`** (partial controller) + **`Api/Models`** + **`ApiControllerBase`** helpers.
6. After **insert or update** that affects read caches, call **`IUserRespository.InvalidateUserCacheAsync`** (or `RemoveCacheKeysAsync` from a write repo base) post-`SaveChanges`.
7. Add **tests** (when you introduce a test project) against handlers, not only controllers.

Following the table above keeps this codebase consistent with both references while keeping our **Result + ApiResponse** and **CQRS read/write** choices explicit.
