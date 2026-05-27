# Data Protection key ring

ASP.NET Core **Data Protection** encrypts sensitive values at rest in this solution. It is **not** used for Api→Worker communication.

---

## What it protects

| Use | Location |
|-----|----------|
| **2FA TOTP secret** in Postgres | `UserConfiguration` → `EncryptedStringConverter` on `TwoFactorSecret` |
| Auth cookies / antiforgery | If enabled by ASP.NET features |

Implementation: `Infrastructure/DependencyInjection.cs` → `AddDataProtection()`.

---

## What it is NOT

- **Not** JWT signing (that uses `JwtSettings:Secret`).
- **Not** RabbitMQ or outbox credentials.
- **Not** a shared secret between Api and Worker for HTTP calls (there are no such calls).

---

## Configuration

```json
"DataProtection": {
  "ApplicationName": "finalproject",
  "KeysDirectory": "",
  "AllowEphemeralKeys": false
}
```

| Key | Meaning |
|-----|---------|
| `ApplicationName` | Must match on **every host** that decrypts the same DB columns |
| `KeysDirectory` | Folder for XML key files; empty → in-memory keys (dev only) |
| `AllowEphemeralKeys` | `true` only for acknowledged single-instance ephemeral setups |

**Production:** `KeysDirectory` **required** (or use Azure Blob / Redis key ring). Startup **fails** in non-Development if directory is missing and `AllowEphemeralKeys` is false.

---

## Local development (`local/dataprotection-keys`)

- Path is in **`.gitignore`** — keys are **secrets**, never commit.
- **Worker** `appsettings.Development.json` sets:
  ```json
  "KeysDirectory": "../../../local/dataprotection-keys"
  ```
- **Api** Development: if `KeysDirectory` is unset, keys are **ephemeral in memory** (restart may break decrypt of existing 2FA secrets in DB).

**Recommendation for team dev:** point **Api** Development at the same folder as Worker so 2FA secrets survive restarts:

```json
"DataProtection": {
  "ApplicationName": "finalproject",
  "KeysDirectory": "../../../local/dataprotection-keys"
}
```

First run **creates** XML files automatically (`key-*.xml`).

---

## New machine / new developer

1. Clone repo (no keys in Git).
2. Set `DataProtection:KeysDirectory` to a writable path (e.g. `local/dataprotection-keys`).
3. Start Api or Worker once → app creates key XML files.
4. **Do not** copy production keys to laptops or vice versa without an explicit ops process.

---

## Server / production deployment

1. Provision a **persistent volume** (e.g. `/var/finalproject/dataprotection-keys`).
2. Set `DataProtection:KeysDirectory` via environment or secret store for **Api and Worker** if both touch encrypted columns.
3. **Backup** the key ring with the same rigor as DB backups — losing keys means **permanent loss** of decrypt ability for existing ciphertext.
4. All Api replicas must share the **same** key ring (shared volume, blob store, or Redis).

---

## Multi-instance checklist

- [ ] Same `ApplicationName`
- [ ] Same key ring storage (not per-pod ephemeral disks)
- [ ] Worker included if it reads/writes `TwoFactorSecret` or other protected columns

---

## Related docs

- [`MESSAGING_AND_WORKER.md`](./MESSAGING_AND_WORKER.md) — Api/Worker boundaries
- [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md) — security overview
