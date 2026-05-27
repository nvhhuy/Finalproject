# Optimistic concurrency (`rowVersion`)

## Domain

- Every type inheriting `Entity<TId>` (via `AggregateRoot<TId>`) has a `RowVersion` property (`uint`).
- EF maps it as a concurrency token (`PersistenceModel.ConfigureRowVersion`).
- On conflicting updates, `SaveChanges` throws → API returns **409** `ConcurrencyConflict`.

Child/owned types (`RefreshToken`, `MediaVariant`, `IdempotencyRecord`, …) do **not** inherit `Entity<>` and have no `RowVersion`.

## API contract (production)

| Operation | Requires `rowVersion`? |
|-----------|-------------------------|
| **GET** user / profile / media asset | Returns `rowVersion` in JSON |
| **PUT/PATCH** profile | Body: `rowVersion` |
| **PUT** admin user | Body: `rowVersion` |
| **DELETE** admin user | Query: `?rowVersion=` |
| **POST** admin mutations (lock, unlock, 2FA reset, password reset, impersonate, email verify, resend, revoke sessions) | Body: `{ "rowVersion": N }` |
| **DELETE** media asset | Query: `?rowVersion=` |
| Register, login, media initiate/complete | No (new rows or idempotent complete) |

### Client flow

1. `GET /api/v1/admin/users/{id}` → read `data.rowVersion`.
2. User edits form (may take minutes).
3. `PUT` / `POST` / `DELETE` with the **same** `rowVersion` from step 1.
4. If another admin changed the user meanwhile → **409** `ConcurrencyConflict` → refresh GET and retry.

The Admin UI (`src/Admin`) passes `rowVersion` on all user mutations from the loaded detail record.

### Error shape

```json
{
  "success": false,
  "error": {
    "code": "ConcurrencyConflict",
    "message": "The record was modified by another request. Reload and retry.",
    "httpStatus": 409
  }
}
```

## Implementation

- Pre-check: `Application.Common.EntityConcurrency.ValidateExpectedVersion` (compares client value to loaded aggregate).
- Post-check: EF `RowVersion` on `SaveChanges` (safety net if two writers race after the pre-check).

## Adding a new aggregate

1. Inherit `AggregateRoot<TId>` (gets `RowVersion` automatically).
2. Add migration column `RowVersion` on the table.
3. Expose `rowVersion` on read DTOs; require it on update/delete commands that matter.
