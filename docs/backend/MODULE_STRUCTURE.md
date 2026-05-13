# Module folder layout (per-domain)

Companion: **`STYLE_FROM_REFERENCE_PROJECTS.md`** — how to write handlers, DTOs, and DI like the Books sample and FaceBookClone.

This backend is organized so each **bounded context / feature domain** lives under predictable paths, similar in spirit to:

- **dotnet-cleanarchitecture-ddd-cqrs-sample** (`Books.Domain/UserAggregate`, `Books.Application/Books/...`) — feature folders + aggregate folders.
- **FaceBookClone** (`Application/Modules/...`, `Domain/Modules/...`, `Infrastructure/Modules/...`) — explicit `Modules` segments for large solutions.

Use this layout when adding **Orders**, **Notifications**, etc.

---

## Domains (`Domains` project)

| Path | Purpose |
|------|---------|
| `Domains/Common/` | Shared kernel: `Entity`, `AggregateRoot`, domain event interfaces, cross-cutting primitives. |
| `Domains/Modules/Users/UserAggregate/` | **Users** bounded context: `User`, `RefreshToken`, `ExternalUserLogin`, value objects. |

**Namespaces:** entity CLR types stay under `Domains.UserAggregate.*` so **EF Core migrations and snapshots** remain stable (table mapping unchanged). Only the **on-disk** path uses `Modules/Users/`.

---

## Application (`Application` project)

| Path | Purpose |
|------|---------|
| `Application/Common/` | Cross-module helpers (`PagedList`, behaviors, shared DTOs). |
| `Application/Contracts/` | Ports not tied to one module (caching, email, messaging, …). |
| `Application/Contracts/Modules/Users/Persistence/` | User persistence ports (`IUserRespository`, read/write repos, `IApplicationReadDbContext`, `IDatabaseTopology`). |
| `Application/Contracts/Settings/` | Runtime app-settings abstraction (`IAppSettingsService`). |
| `Application/Modules/Users/Authentication/` | Login, register, refresh, Google, 2FA completion. |
| `Application/Modules/Users/Administration/` | Admin commands/queries on users. |
| `Application/Modules/Users/Profile/` | Current-user profile, 2FA setup, user-scoped domain events. |
| `Application/Modules/SystemSettings/Admin/` | Admin-facing app setting queries/commands (feature toggles). |
| `Application/Mappings/` | AutoMapper profiles (can later split to `Modules/Users/Mappings` if desired). |

**Namespaces:** `Application.Modules.Users.{Authentication|Administration|Profile}.…` and `Application.Contracts.Modules.Users.Persistence…`.

---

## Infrastructure (`Infrastructure` project)

| Path | Purpose |
|------|---------|
| `Infrastructure/Persistence/Repositories/Base/` | **`RepositoryCacheBase`**, **`CachedReadRepositoryBase`**, **`WriteRepositoryBase`** — shared cache/remove helpers + reusable generic read/write EF methods. |
| `Infrastructure/Persistence/` | Shared EF: `ApplicationDbContext`, `ApplicationReadDbContext`, migrations, interceptors, `PersistenceModel`, seeding. |
| `Infrastructure/Modules/Users/Persistence/Repositories/` | User read/write repositories and cache keys. |
| `Infrastructure/Modules/Users/Persistence/Configuration/` | EF `IEntityTypeConfiguration<>` for user entities. |
| `Infrastructure/Settings/` | `AppSettingsService` implementation over `AppSettings` table. |
| Other top-level folders | Auth, caching, media, messaging, etc. (split into `Modules/...` when those areas grow). |

---

## Api (`Api` project)

HTTP controllers are grouped by feature (FaceBookClone-style):

| Folder | Namespace | Contents |
|--------|-----------|----------|
| `Api/Controllers/Base/` | `Api.Controllers.Base` | `ApiControllerBase` (shared `Sender`, `ApiResponse<T>` mapping). |
| `Api/Controllers/Authentication/` | `Api.Controllers.Authentication` | `AuthController`. |
| `Api/Controllers/Users/` | `Api.Controllers.Users` | `UsersController` (+ `.Read` / `.Write` partials). |
| `Api/Controllers/Admin/` | `Api.Controllers.Admin` | `AdminUsersController` (+ partials), `AdminSettingsController` (runtime feature toggles). |
| `Api/Controllers/Samples/` | `Api.Controllers.Samples` | `SamplesController` (+ transaction demo partial). |

**HTTP models** live under `Api/Models/`. ASP.NET Core still discovers all `*Controller` types in the assembly regardless of subfolder.

---

## Adding a new module (checklist)

1. **Domains:** `Domains/Modules/{Name}/…` (+ decide namespace: new `Domains.{Name}` or sub-aggregate folders).
2. **Application:** `Application/Modules/{Name}/…` for commands/queries/handlers; `Application/Contracts/Modules/{Name}/…` for ports.
3. **Infrastructure:** `Infrastructure/Modules/{Name}/…` for EF configs/repos that belong only to that module; register services in `DependencyInjection.cs`.
4. **Api:** controllers + `Api/Models` contracts.
5. **EF:** add migration from **write** context; keep entity type names stable if you already shipped a DB.

---

## Reference repos (local paths on your machine)

- `~/Desktop/BaseDDDDotnet/dotnet-cleanarchitecture-ddd-cqrs-sample/src` — clean vertical slices per feature in Application, aggregate folders in Domain.
- `~/Desktop/DuyFacebookClone/FaceBookClone/Src/backend` — `Application/Modules/*`, `Infrastructure/Modules/*`, API grouped by feature.

Align new code with **one module = one folder tree** across layers to keep navigation and ownership obvious.
