# Caching Guide

This document describes the **three cache patterns** used in the codebase and **the rule for placing cache in the correct layer**. Read together with `DEVELOPER_GUIDE.md` §3b.

> **One-line rule:**
> Cache **entity reads from DB** → repository. Cache **short-lived business state** → handler. Cache **expensive computed results** → query handler / read service.

---

## 1. Infrastructure

| Component | Location | Role |
|---|---|---|
| `ICacheService` | `Application/Contracts/Caching/ICacheService.cs` | Common port (get / set / remove + TTL) |
| `RedisCacheService` | `Infrastructure/...` (when Redis is enabled) | Redis adapter |
| `MemoryCacheService` | `Infrastructure/...` (default in dev) | In-memory adapter |
| `CachedReadRepositoryBase` | `Infrastructure/Persistence/Repositories/Base/` | Read-through helper for repositories |
| `WriteRepositoryBase` | `Infrastructure/Persistence/Repositories/Base/` | Invalidation helper for write repositories |

All three patterns below share **the same `ICacheService`** — they differ only in **where the cache lives** and **what it means**.

---

## 2. Pattern 1 — Read-through cache in the **Repository**

**Use when:** an entity is read from the database many times by the same key (id, email, username...).

**Characteristics:**
- DB is the source of truth.
- Cache is only a speed-up layer.
- TTL is short to medium (minutes).
- Invalidate immediately after every mutation.

### How to implement

**Step 1 — Declare cache keys** in `Infrastructure/.../Repositories/<Entity>ReadCacheKeys.cs`:

```csharp
internal static class UserReadCacheKeys
{
    public static string ById(Guid id) => $"read:User:id:{id}";
    public static string ByEmail(string email) => $"read:User:email:{email.ToLowerInvariant()}";
}
```

**Step 2 — Wrap the query inside the read repository:**

```csharp
public sealed class UserReadRepository : CachedReadRepositoryBase, IUserReadRepository
{
    private static readonly TimeSpan IdTtl = TimeSpan.FromMinutes(10);

    public Task<User?> GetByIdAsync(Guid id, CacheOptions cache = default, CancellationToken ct = default) =>
        GetSingleCachedByPredicateAsync(
            UserReadCacheKeys.ById(id),
            _read.Users,
            u => u.Id.Value == id,
            cache,
            IdTtl,
            ct);
}
```

**Step 3 — Invalidate inside the write repository** (after the handler calls `_uow.SaveChangesAsync(ct)`):

```csharp
// IUserRespository
await _users.InvalidateUserCacheAsync(userId, email, userName, ct);
```

### What the handler sees

**Fully transparent** — the handler does not know whether the read came from cache or DB:

```csharp
User? user = await _users.GetUserByEmailAsync(email);
```

### Codebase examples

- `Infrastructure/Modules/Users/Persistence/Repositories/UserReadRepository.cs`
- `Infrastructure/Modules/Users/Persistence/Repositories/UserReadCacheKeys.cs`
- `Infrastructure/Modules/Users/Persistence/Repositories/UserWriteRepository.cs` (invalidation)

---

## 3. Pattern 2 — Ephemeral business state in the **Handler**

**Use when:** you need to remember a short-lived business artifact that has **no row in the database**.

**Characteristics:**
- **The cache is the source of truth** until the key expires or is removed.
- TTL equals the business lifecycle (for example "token valid for 24h").
- Delete the key explicitly when the artifact is consumed (one-time use).

### How to implement

**Step 1 — Declare the prefix** in `Application/Modules/.../<Module>CacheKeys.cs`:

```csharp
public static class AuthCacheKeys
{
    public const string PendingLoginPrefix = "auth:login-pending:";
    public const string PasswordResetPrefix = "auth:password-reset:";
    public const string EmailVerificationPrefix = "auth:email-verify:";
}
```

**Step 2 — Producer handler** (the handler that issues the token):

```csharp
string tokenId = Guid.NewGuid().ToString("N");
string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
string tokenHash = TokenHasher.Sha256(token);
string cacheKey = AuthCacheKeys.PasswordResetPrefix + tokenId;

await _cache.SetAsync(
    cacheKey,
    new PasswordResetTokenPayload(user.Id.Value, tokenHash),
    TimeSpan.FromMinutes(30),
    ct);
```

**Step 3 — Consumer handler** (the handler that verifies and removes the token):

```csharp
var payload = await _cache.GetAsync<PasswordResetTokenPayload>(cacheKey, ct);
if (payload is null)
    return new Error("InvalidOrExpired", 400, "Token is invalid or expired.");

if (!string.Equals(payload.TokenHash, hash, StringComparison.Ordinal))
    return new Error("InvalidOrExpired", 400, "Token is invalid or expired.");

await _cache.RemoveAsync(cacheKey, ct);
```

### Codebase examples

- `Application/Modules/Users/Authentication/AuthCacheKeys.cs`
- `Application/Modules/Users/Authentication/Commands/VerifyEmail/VerifyEmailCommands.cs`
- `Application/Modules/Users/Authentication/Commands/ForgotPassword/ForgotPasswordCommands.cs`
- Two-factor pending challenge in `CompleteTwoFactorLoginCommandHandler.cs`

---

## 4. Pattern 3 — Cache-aside for expensive computed results

**Use when:** a query / aggregation is expensive (dashboard metrics, top-N, complex counts...) and the data **can tolerate brief staleness**.

**Characteristics:**
- DB remains the source of truth, but you **do not cache by entity** — you cache **by query result**.
- The cache key depends on **the query parameters** (filter, date range, user id...).
- TTL matches the acceptable freshness window (30s – a few minutes).
- **No precise invalidation needed** — wait for the TTL to expire.

### How to implement

Put the logic in the **query handler** (not in the repository, because the cache key depends on query inputs, not on a single entity):

```csharp
public sealed class AdminDashboardMetricsQueryHandler
    : IRequestHandler<AdminDashboardMetricsQuery, Result<AdminDashboardMetricsDto, Error>>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);
    private readonly ICacheService _cache;
    private readonly IApplicationReadDbContext _read;

    public async Task<Result<AdminDashboardMetricsDto, Error>> Handle(
        AdminDashboardMetricsQuery req, CancellationToken ct)
    {
        string cacheKey = $"metrics:dashboard:{req.Days}";

        var cached = await _cache.GetAsync<AdminDashboardMetricsDto>(cacheKey, ct);
        if (cached is not null)
            return cached;

        // ...heavy DB query + aggregation...
        AdminDashboardMetricsDto dto = await ComputeMetricsAsync(req.Days, ct);

        await _cache.SetAsync(cacheKey, dto, Ttl, ct);
        return dto;
    }
}
```

### Good fits

- Expensive aggregations: `GROUP BY`, `COUNT(DISTINCT)`, multi-join.
- Read models for dashboards, top lists, leaderboards.
- Search results / filtered lists with pagination.

### Bad fits

- Real-time data (balance, inventory).
- Highly personalized data — the cache key space would explode.

---

## 5. Quick comparison of the three patterns

| Aspect | Pattern 1 — Repo | Pattern 2 — Handler | Pattern 3 — Query handler |
|---|---|---|---|
| Purpose | Speed up entity reads | Hold tokens / challenges | Speed up aggregations |
| Source of truth | DB | **Cache** | DB |
| TTL | Minutes | Business lifecycle | 30s – a few minutes |
| Invalidation | After every entity mutation | When the token is consumed | Let the TTL expire |
| Key | By PK / unique field | Random token id + prefix | By hashed query input |
| Does the handler know about cache? | **No** | **Yes** (explicit) | **Yes** (explicit) |
| Code layer | `Infrastructure/.../Repositories` | `Application/Modules/.../Handlers` | `Application/Modules/.../Queries` |

---

## 6. Anti-patterns to avoid

| Smell | Why it hurts |
|---|---|
| Caching `User` from a handler with `_cache.Set("user:" + id, user)` | Read-through belongs to the repo. Other handlers won't see the cache; on update, who is responsible for invalidation? |
| Repository setting TTL based on a business rule (e.g. "token lives 24h") | The repo leaks business semantics — violates Clean Architecture. |
| Repository invalidating keys owned by a handler (tokens, challenges) | A repo must only invalidate caches **it populated itself**. |
| Caching heavy query results **inside a repo** | A repo caches by entity. Caching by query input belongs in the query handler. |
| Reading the cache directly inside a controller | Controllers orchestrate only; bypassing layers breaks the architecture. |
| Forgetting to invalidate after updating an entity | Stale reads — clients see the old version after a mutation. |
| Not removing the key after a one-time token is consumed | The token becomes reusable — a security hole. |

---

## 7. Checklist when adding a new cache

Ask three questions before writing code:

1. **Is this an entity read from the DB?** → Pattern 1 (repository).
2. **Is this short-lived state with no DB row?** → Pattern 2 (handler).
3. **Is this an expensive query result that tolerates brief staleness?** → Pattern 3 (query handler).

Then:

- [ ] Declare the cache key as a constant in the relevant module/repo (no hardcoded strings inside handlers).
- [ ] Set a sensible TTL (Pattern 1: minutes; Pattern 2: matches business lifecycle; Pattern 3: 30s – a few minutes).
- [ ] Pattern 1: update the `Invalidate...Async` helper in the write repository.
- [ ] Pattern 2: remember to call `RemoveAsync` on consumption.
- [ ] Pattern 3: a TTL is enough — no complex invalidation needed.
- [ ] Log / measure cache hit ratio if it sits on a hot endpoint.

---

## 8. File reference

| Pattern | Example file |
|---|---|
| Pattern 1 (repo) | `src/Backend/Infrastructure/Modules/Users/Persistence/Repositories/UserReadRepository.cs` |
| Pattern 1 (keys) | `src/Backend/Infrastructure/Modules/Users/Persistence/Repositories/UserReadCacheKeys.cs` |
| Pattern 1 (invalidate) | `src/Backend/Infrastructure/Modules/Users/Persistence/Repositories/UserWriteRepository.cs` |
| Pattern 2 (verify email) | `src/Backend/Application/Modules/Users/Authentication/Commands/VerifyEmail/VerifyEmailCommands.cs` |
| Pattern 2 (password reset) | `src/Backend/Application/Modules/Users/Authentication/Commands/ForgotPassword/ForgotPasswordCommands.cs` |
| Pattern 2 (keys) | `src/Backend/Application/Modules/Users/Authentication/AuthCacheKeys.cs` |
| Pattern 3 | *(not yet present — add when you need a cached dashboard / read model)* |
