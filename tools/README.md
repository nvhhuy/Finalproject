# Repo tools (not shipped in app binaries)

Scripts and CLIs live **outside** `src/Backend/` and `src/Frontend/` source trees so they stay easy to find and safe to change without touching product code.

| Path | Purpose |
|------|---------|
| [`backend/migrate.sh`](./backend/migrate.sh) | Apply EF Core migrations to the **write** database (`ApplicationDbContext`). Run from dev machine or CI. |
| `frontend/` | Reserve for npm/node one-off scripts (codegen, lint fixes, etc.). |

### Run database migrations

From repository root:

```bash
./tools/backend/migrate.sh
```

Requires [.NET SDK](https://dotnet.microsoft.com/download) and the `src/Backend` layout (`Infrastructure` + `Api` projects).
