# EF Core LINQ pitfalls (PostgreSQL)

Patterns that **fail at runtime** with `InvalidOperationException: could not be translated` and the **correct** fixes used in this codebase.

---

## 1. Value object `.Id.Value` in `Where`

### Symptom

```
.Where(u => u.Id.Value == __id_0)' could not be translated
```

### Cause

`UserId` / `MediaAssetId` are value objects with EF value converters. Navigating `.Value` inside LINQ often breaks translation.

### Fix

Compare the **whole value object** using the factory:

```csharp
// Good — UserReadRepository, UserWriteRepository
u => u.Id == UserId.Create(id)

// Good — AuthenticationTokenIssuer
u => u.Id == user.Id

// Good — GoogleSignInCommandHandler
u => u.Id == link.UserId
```

```csharp
// Bad in LINQ
u => u.Id.Value == id
```

### Audit when adding code

Search: `\.Id\.Value\s*==` in `Infrastructure/` and `Application/` before merge.

---

## 2. `DateOnly` / `DateOnly.FromDateTime` in SQL `GroupBy`

### Symptom

```
.GroupBy(u => DateOnly.FromDateTime(u.RegisteredAtUtc.Value.Date))' could not be translated
```

### Fix (`AdminDashboardMetricsQuery`)

Group by **`DateTime.Date`** in SQL, map to `DateOnly` **after** `ToListAsync`:

```csharp
var dailyCounts = await _read.Users.AsNoTracking()
    .Where(...)
    .GroupBy(u => u.RegisteredAtUtc!.Value.Date)
    .Select(g => new { Day = g.Key, Count = g.Count() })
    .ToListAsync(ct);

var daily = dailyCounts
    .Select(x => new DailyRegistrationDto(DateOnly.FromDateTime(x.Day), x.Count))
    .ToList();
```

Alternative for complex reports: raw SQL (`FromSqlInterpolated`) or database views.

---

## 3. `g.Count()` after `GroupBy` with DTO projection

Avoid:

```csharp
.Select(g => new DailyRegistrationDto(g.Key, g.AsQueryable().Count()))
```

Use `g.Count()` directly in a simple anonymous projection, then map to DTO in memory if needed.

---

## 4. Read context writes

`ApplicationReadDbContext` overrides `SaveChanges` to **throw**. Mutations must use:

- `ApplicationDbContext` / write repositories / `IUnitOfWork` on the write side.

---

## 5. Explicit transactions + Npgsql retry strategy

### Symptom

```
NpgsqlRetryingExecutionStrategy does not support user-initiated transactions
```

### Fix

Wrap transaction work in:

```csharp
await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { ... });
```

Already applied in:

- `TransactionalActionFilter` (Api)
- `OutboxPublisherHostedService` (Worker)

Any new code that calls `BeginTransactionAsync` manually must use the same pattern.

---

## 6. Client evaluation escape hatches

`AsEnumerable()`, `ToListAsync()` then LINQ in memory — use **only** when:

- Dataset is bounded (admin metrics 14 days, single user lookup is fine via proper predicate).
- You accept loading more rows than necessary.

Prefer translatable SQL for list endpoints at scale.

---

## 7. Normalized lookups (Users)

For email/username search, filter on **`NormalizedEmail`** / **`NormalizedUserName`** with the same normalization as the aggregate (`ToUpperInvariant()`), not raw `Email`/`UserName` equality — see `UserReadRepository`.

---

## Checklist for new queries

- [ ] No `.Value` on converted value objects in `IQueryable` predicates
- [ ] No `DateOnly` factory methods inside SQL-translated `GroupBy`/`Select`
- [ ] Read repo = no tracking, no save
- [ ] Transactions use execution strategy when retry is enabled
- [ ] Integration or handler test exercises the query against PostgreSQL when non-trivial

---

## Related docs

- [`CACHE_GUIDE.md`](./CACHE_GUIDE.md) — read-through cache (prefer `UserId.Create` in predicates)
- [`OPTIMISTIC_CONCURRENCY.md`](./OPTIMISTIC_CONCURRENCY.md) — `rowVersion` / `xmin`
