# EF Core migration workflow

This document is the team's source of truth for **creating, rebasing, and reviewing EF Core migrations**. It exists because EF Core's `ApplicationDbContextModelSnapshot.cs` is a single shared file regenerated on every `migrations add`, which means parallel branches that both add migrations always conflict in that file — and naive merges silently produce a schema that matches neither branch.

The tooling under `tools/backend/migration/` makes the safe workflow easy, and the CI gate in `.github/workflows/migration-check.yml` blocks PRs that skipped it.

---

## How migrations flow through environments

```
[Developer local]
      |
      |   1. Edit entity code (e.g. add MediaAsset.Tags)
      |   2. ./tools/backend/migration/new.sh AddMediaAssetTags
      |   3. Local Postgres applies it automatically (Development:
      |      Database:ApplyPendingMigrations = true; see appsettings.Development.json)
      v
[Git commit + push to branch]
      |
      |   *** Migration files ARE COMMITTED TO GIT like any other code ***
      |   Three files per migration:
      |     - 20260527120000_AddMediaAssetTags.cs           (Up/Down DDL)
      |     - 20260527120000_AddMediaAssetTags.Designer.cs  (snapshot AT this migration)
      |     - ApplicationDbContextModelSnapshot.cs           (latest cumulative snapshot)
      v
[PR review + CI gate (.github/workflows/migration-check.yml)]
      |
      |   - Spins fresh Postgres
      |   - Applies every migration from scratch
      |   - Runs `dotnet ef migrations has-pending-model-changes` (must return 0)
      |   - Validates timestamps don't collide with main's
      v
[Dev / staging / production servers]

      Same migration files travel with the code.
      Each environment's __EFMigrationsHistory table tracks which migrations
      have been applied THERE. The migration files themselves are universal.
```

Key invariants — break these and CI rejects the PR:

1. **Migration files are committed to git.** They are not local-only artifacts.
2. **Timestamps are strictly increasing.** A migration added on a stale branch will have a timestamp older than `origin/main`'s newest; that breaks the apply order everywhere.
3. **`ApplicationDbContextModelSnapshot.cs` is staged together with every migration change.** EF regenerates it on every `migrations add`; forgetting to stage it leaves the source-of-truth out of sync.

---

## Tooling overview

```
tools/backend/migration/
├── new.sh        # Create a new migration (preflighted)
├── rebase.sh     # Regenerate pending migrations on top of origin/main
├── check.sh      # Drift detector (fast | full)  ← also runs in CI
└── lib/common.sh # Shared helpers

tools/backend/git-hooks/
├── pre-commit    # Auto-runs check.sh --fast on migration commits
└── install.sh    # Wires hooks into git via core.hooksPath
```

**One-time setup after cloning:**

```bash
./tools/backend/git-hooks/install.sh
```

This points `core.hooksPath` at the tracked hooks directory so the pre-commit guard runs automatically. The install is per-clone — every team member runs it once.

---

## Daily workflows

### Create a new migration

```bash
./tools/backend/migration/new.sh AddMediaAssetTags
```

What happens:

1. Verifies your working tree is clean.
2. Verifies your branch is up to date with `origin/main` (refuses to proceed otherwise — pass `--allow-stale` to override).
3. Verifies entity code actually has changes (otherwise the migration would be empty).
4. Runs `dotnet ef migrations add AddMediaAssetTags`.
5. Runs the fast drift check.
6. Prints the three commands you need next: apply, stage, commit.

### Apply migrations to your local DB

```bash
./tools/backend/migrate.sh
```

(Pre-existing script. Equivalent to `dotnet ef database update`.)

### Rebase your migration onto the latest main

Use this when:

- A teammate merged a migration to main while your PR was open.
- Your branch is behind `origin/main` and you need to bring it forward.
- The CI drift check failed with "Your newest local migration is older than origin/main's newest".

```bash
git fetch origin
./tools/backend/migration/rebase.sh
```

What happens (every destructive step asks for confirmation):

1. Reverts your local DB to the last migration shared with `origin/main`.
2. Removes your pending migrations via `dotnet ef migrations remove`.
3. Runs `git rebase origin/main`.
4. Re-creates the migrations with fresh timestamps + a snapshot that includes everything from main.
5. Prints the exact `git add` / `git commit --amend` / `git push --force-with-lease` you need.

If the `git rebase` step hits real code conflicts (not migration files), the script stops and tells you to resolve them by hand, then re-run with `--skip-rebase`.

### Manual drift check

```bash
./tools/backend/migration/check.sh           # fast (static) — sub-second
./tools/backend/migration/check.sh --full    # full (build + apply + drift)
```

Use `--fast` constantly (the pre-commit hook does this automatically). Use `--full` before opening a tricky PR; CI runs `--full` on every PR that touches schema-related files.

---

## Running the CI gate on Gitea (or any non-GitHub host)

The workflow under `.github/workflows/migration-check.yml` was authored against the GitHub Actions schema, but **Gitea Actions** (Gitea 1.19+) reads the same file with no edits — the `.github/workflows/` folder is honoured by both. See [`CI_GITEA_SETUP.md`](./CI_GITEA_SETUP.md) for the full runner-deploy walk-through, including:

- Enabling Actions in Gitea's `app.ini`.
- Spinning an `act_runner` via `tools/ci/gitea-runner.compose.yml`.
- Air-gapped instances (mirroring `actions/*` into Gitea).
- Troubleshooting "runner stays Pending", "actions/checkout not found", "service container never healthy".

For other CI systems (Drone, Woodpecker, Jenkins, GitLab CI), the underlying script — `tools/backend/migration/check.sh --full` — is what does the actual checking. Wire it into any CI by:

1. Spinning a Postgres container reachable on `localhost:5432`.
2. Setting `DRIFT_CHECK_CONNECTION="Host=localhost;Port=5432;Database=drift_check;Username=ci;Password=ci"`.
3. Setting `MIGRATION_BASE_REF="origin/main"` (or whatever your trunk branch is called).
4. Running `./tools/backend/migration/check.sh --full`.

That's the same contract `migration-check.yml` uses; the YAML is just glue.

---

## When the CI gate fails

The workflow `.github/workflows/migration-check.yml` runs `check.sh --full` on every PR. Common failures and their fixes:

| CI message | Cause | Fix |
|---|---|---|
| `Your newest local migration (T1) is older than origin/main's newest (T2)` | You branched off main, then teammates merged a migration. Your timestamp is now stale. | `./tools/backend/migration/rebase.sh` |
| `Unresolved git merge markers found` | You accepted a merge into `ApplicationDbContextModelSnapshot.cs` and committed without resolving. | Revert the snapshot, run `rebase.sh` to regenerate cleanly. |
| `Model code is ahead of the migration snapshot` | You edited an entity but forgot to run `migrations add`. | `./tools/backend/migration/new.sh <Name>` |
| `Designer file present but migration missing` (or vice versa) | Partial commit — you staged only one of the pair. | `git add src/Backend/Infrastructure/Persistence/Migrations/` (the whole folder) and re-commit. |
| `You staged migration files but ApplicationDbContextModelSnapshot.cs is not staged` | EF regenerated the snapshot but you didn't `git add` it. | `git add src/Backend/Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs`, amend the commit. |
| `dotnet ef database update failed` against the sandbox DB | The migration's SQL doesn't apply cleanly (e.g. dropping a column that doesn't exist on a fresh DB). | Inspect the Up/Down methods; the migration must be runnable on an empty DB. |

The workflow uploads drift logs from `${DRIFT_LOG_DIR}` as artifacts on failure (default in CI:
`/tmp/migration-drift-<run-id>-<attempt>/`). Download them from the failed run's
"Summary" page for the full `dotnet` output.

---

## Manual recovery: when the scripts can't help

There are two failure modes the scripts can't fix automatically. Both are rare and worth documenting.

### A) Conflict markers in `ApplicationDbContextModelSnapshot.cs`

A teammate merged migration `M2` to main. You merged main into your branch (instead of rebasing) and got conflict markers in the snapshot file because both branches had different `b.HasAnnotation("ProductVersion", ...)` lines and entity blocks.

Do **not** hand-edit the snapshot to reconcile the diffs. The conflict means EF has lost track of which model state corresponds to which migration. Recover with:

```bash
# 1. Abort the merge.
git merge --abort                          # or `git reset --hard HEAD` if already committed

# 2. Switch to rebase mode and use the tooling.
git fetch origin
git rebase origin/main                     # resolve any non-snapshot conflicts manually
./tools/backend/migration/rebase.sh --skip-rebase
```

### B) `__EFMigrationsHistory` out of sync with files

Symptom: `dotnet ef database update` fails with `The migration '20260527120000_Foo' has already been applied to the database.` even after you removed the file.

Cause: somebody applied a migration locally, then someone else force-pushed the branch with a different migration ID after rebase. The DB's history table still references the old ID; the files reference the new one.

Fix (local DB only — never do this in shared environments):

```bash
# Option 1: drop + recreate the local DB.
dropdb baseproject && createdb baseproject
./tools/backend/migrate.sh

# Option 2: manually remove the stale row.
psql -d baseproject -c "DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260527120000_Foo';"
./tools/backend/migrate.sh
```

---

## Periodic squash baseline (every major release)

The `Migrations/` directory grows monotonically — we keep adding files, never deleting. Over time this:

- Slows down `dotnet ef` invocations (it has to load every Designer.cs).
- Makes onboarding harder (new devs see 60+ migration files for changes they don't care about).
- Increases the conflict surface in `ApplicationDbContextModelSnapshot.cs`.

Per release, the team lead (or a designated "schema steward") consolidates the history into a single baseline:

1. **Coordinate on Slack/Teams**: announce a freeze window, ~30 min, no migration PRs.
2. **Tag the current state** so we can roll back if anything goes wrong:
   ```bash
   git tag pre-migration-squash-v$(date +%Y%m%d)
   git push --tags
   ```
3. **In every long-lived environment (dev / staging / prod), record the current latest migration**:
   ```sql
   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 1;
   ```
   Save this ID — you'll need it to seed `__EFMigrationsHistory` after the squash.
4. **Locally**: delete every file under `Migrations/` and regenerate:
   ```bash
   rm src/Backend/Infrastructure/Persistence/Migrations/*.cs
   ./tools/backend/migration/new.sh InitialBaseline_v$(date +%Y%m)
   ```
5. **Manually seed each environment's history** so EF doesn't try to re-apply the baseline against a DB that already has the schema:
   ```sql
   INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
   VALUES ('<new_baseline_timestamp>_InitialBaseline_v<...>', '8.0.x');
   DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" != '<new_baseline_timestamp>_...';
   ```
   (Do this BEFORE you deploy — otherwise the application boots and EF would try to apply the new baseline against an already-populated DB.)
6. Open a single PR with the new baseline. CI's drift check will pass because the snapshot perfectly matches the model. Reviewers spot-check that the generated SQL matches what production already has.

Frequency: aim for **one squash per major release** (every 2-3 months for an active project). More often is busywork; less often defeats the point.

---

## Why we don't auto-rebase in the pre-commit hook

The first design considered was a hook that detects an out-of-date branch and automatically reverts the DB, removes migrations, rebases, and regenerates. We rejected it for these reasons:

1. **`dotnet ef migrations remove` requires the DB to be at a known state.** A hook can't safely revert a DB that the dev's other dev tools are also using (e.g. the API running locally).
2. **Pre-commit must be fast.** `dotnet build` + EF tools is 30+ seconds; nobody tolerates that on every commit.
3. **Auto-rebase produces silent history rewrites.** When `git rebase` hits a real conflict, an automated tool can't make the call about how to resolve it.
4. **Filename instability breaks code review.** Auto-regenerating migrations changes their timestamps; PR diffs become unreadable as files appear renamed.

The hook stays in the "detect & guide" lane: it catches what's about to break and tells you exactly which one-line script will fix it. The actual fix runs interactively (and visibly) via `rebase.sh`.

---

## Quick reference

```bash
# One-time setup
./tools/backend/git-hooks/install.sh

# Daily
./tools/backend/migration/new.sh <PascalCaseName>
./tools/backend/migrate.sh

# When CI complains
./tools/backend/migration/rebase.sh
git push --force-with-lease

# Sanity check
./tools/backend/migration/check.sh           # quick
./tools/backend/migration/check.sh --full    # thorough
```
