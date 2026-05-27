#!/usr/bin/env bash
# Migration drift detector. Runs the same set of guards both locally (via
# pre-commit hook or manual invocation) and in CI. Two modes:
#
#   --fast (default)
#       Static checks only — no Postgres, no `dotnet ef`. Sub-second runtime.
#       Catches the failure modes that 95% of real-world conflicts surface as:
#         * unresolved git merge markers in migration / snapshot files
#         * orphan migration .cs without matching .Designer.cs (or vice versa)
#         * duplicate migration names (case-insensitive)
#         * non-monotonic timestamps within the local branch
#         * local migration timestamp < newest on origin/main
#           (signals a rebase that wasn't followed by `migrations remove + add`)
#
#   --full
#       Everything above PLUS:
#         * dotnet build (validates model code compiles after merge)
#         * `dotnet ef database update` on a freshly-created Postgres database
#         * `dotnet ef migrations has-pending-model-changes`
#       Use this in CI; locally only when you suspect snapshot drift.
#
# Exit codes:
#   0  — all checks passed
#   1  — a check failed (message printed to stderr)
#   2  — usage / environment error (missing tool, etc.)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib/common.sh"

MODE="fast"
if [[ $# -gt 0 ]]; then
    case "$1" in
        --fast|--full) MODE="${1#--}" ;;
        -h|--help)
            cat <<'EOF'
Usage: check.sh [--fast | --full]

  --fast   Static checks only (default). No DB or dotnet required.
  --full   Static + dotnet build + apply migrations on a fresh DB + drift detection.
           Requires Postgres reachable via the connection string in appsettings,
           or via the ConnectionStrings__DatabaseConnectionString env var.

Env vars:
  MIGRATION_BASE_REF   Trunk branch to diff against. Defaults to origin/main.
EOF
            exit 0
            ;;
        *)
            log_error "Unknown argument: $1 (use --help)"
            exit 2
            ;;
    esac
fi

cd "$REPO_ROOT"

# Use a run-specific log directory so concurrent self-hosted jobs don't clobber
# each other's diagnostics. CI can override with DRIFT_LOG_DIR for predictable
# artifact collection.
DRIFT_LOG_DIR="${DRIFT_LOG_DIR:-/tmp/migration-drift-${PPID}-$$}"
mkdir -p "$DRIFT_LOG_DIR"
RESTORE_LOG="$DRIFT_LOG_DIR/restore.log"
BUILD_LOG="$DRIFT_LOG_DIR/build.log"
PENDING_LOG="$DRIFT_LOG_DIR/pending.log"
APPLY_LOG="$DRIFT_LOG_DIR/apply.log"

# Track how many checks failed so we can summarise at the end instead of
# bailing on the first failure — devs prefer seeing every problem at once.
FAIL_COUNT=0
record_fail() {
    FAIL_COUNT=$((FAIL_COUNT + 1))
    log_error "$1"
}

# ─────────────────────────────────────────────────────────────────────────────
# Static checks (fast mode)
# ─────────────────────────────────────────────────────────────────────────────

log_step "Migration drift check (${MODE})"

if [[ ! -d "$MIGRATIONS_DIR" ]]; then
    log_warn "Migrations directory not found: $MIGRATIONS_DIR"
    log_warn "Nothing to check. Exiting cleanly."
    exit 0
fi

# (1) Unresolved git merge markers anywhere under Migrations/. These appear when
# a dev accepts a merge that touched ApplicationDbContextModelSnapshot.cs and
# tries to commit without resolving — easiest class of mistake to catch.
log_info "(1/6) Scanning for unresolved git merge markers..."
# `git grep` only sees tracked files (good — untracked conflicts get caught at
# `git add` time). We scan both tracked AND just-added-but-uncommitted content
# by combining the working tree pass with a staged-diff pass.
# The patterns:
#   <<<<<<< ref-name        — opening, has trailing space + ref
#   =======                 — separator, no trailing content
#   >>>>>>> ref-name        — closing, has trailing space + ref
# Matching on 7 chars at start-of-line catches all three reliably.
MERGE_MARKER_REGEX='^(<{7}|={7}|>{7})( |$)'
if git -C "$REPO_ROOT" grep -nE "$MERGE_MARKER_REGEX" -- "$MIGRATIONS_DIR" 2>/dev/null; then
    record_fail "Unresolved git merge markers found in migration files (see above)."
    record_fail "Resolve them manually, then re-run this check. NEVER hand-edit ApplicationDbContextModelSnapshot.cs to satisfy the diff: regenerate via ./tools/backend/migration/rebase.sh."
fi
# Also scan staged-but-uncommitted blobs (for the pre-commit hook flow).
if git -C "$REPO_ROOT" diff --cached --name-only -- "$MIGRATIONS_DIR" 2>/dev/null | while IFS= read -r staged; do
        [[ -n "$staged" ]] || continue
        if git -C "$REPO_ROOT" show ":$staged" 2>/dev/null | grep -nE "$MERGE_MARKER_REGEX" >/dev/null; then
            echo "$staged"
        fi
    done | grep -q . ; then
    record_fail "Staged migration files contain unresolved merge markers — commit blocked."
fi

# (2) Every migration `.cs` must be paired with a `.Designer.cs`. The pair gets
# generated together by `dotnet ef migrations add` so a missing one always
# indicates a partial commit (e.g. dev added the .cs but git missed the
# .Designer.cs because it was unstaged).
log_info "(2/6) Verifying every migration has a Designer counterpart..."
while IFS= read -r migration; do
    if [[ ! -f "$MIGRATIONS_DIR/${migration}.cs" ]]; then
        record_fail "Designer file present but migration missing: ${migration}.Designer.cs"
    fi
    if [[ ! -f "$MIGRATIONS_DIR/${migration}.Designer.cs" ]]; then
        record_fail "Migration present but Designer file missing: ${migration}.cs"
    fi
done < <(list_local_migrations)

if [[ ! -f "$MODEL_SNAPSHOT_FILE" ]]; then
    record_fail "ApplicationDbContextModelSnapshot.cs is missing. Run \`dotnet ef migrations add\` once to regenerate."
fi

# (3) Duplicate migration names (case-insensitive) — EF tools allow them but
# they produce confusing `__EFMigrationsHistory` rows and break downstream
# tooling that uses the name as a key.
log_info "(3/6) Checking for duplicate migration names..."
DUPLICATES="$(list_local_migrations | awk -F'_' '{$1=""; sub(/^[ _]+/, ""); print tolower($0)}' | sort | uniq -d)"
if [[ -n "$DUPLICATES" ]]; then
    record_fail "Duplicate migration names found: $DUPLICATES"
fi

# (4) Timestamps must increase strictly. EF apply order is filename-sorted, so a
# regression means a later commit's migration would attempt to run BEFORE one
# that's already in `__EFMigrationsHistory` from earlier — which fails on every
# DB except a freshly-created one.
log_info "(4/6) Verifying migration timestamps are strictly monotonic..."
PREV=""
while IFS= read -r migration; do
    ts="$(migration_timestamp "$migration")"
    if [[ -n "$PREV" && "$ts" == "$PREV" ]]; then
        record_fail "Two migrations share timestamp $ts. Regenerate one with \`dotnet ef migrations remove\` then \`migrations add\`."
    fi
    PREV="$ts"
done < <(list_local_migrations)

# (5) Latest local migration timestamp >= latest on origin/main. This catches
# the "I forgot to rebase" case: a dev branches off main on day 1, creates
# migration with timestamp T1, peer merges a different migration to main at
# T2 (T2 > T1), and the dev opens a PR without rebasing. EF apply order would
# now try to run their migration (T1) AFTER their peer's (T2), but the dev's
# Designer.cs snapshot doesn't know about T2.
log_info "(5/6) Comparing against $(base_ref)..."
if ensure_base_ref; then
    MAIN_MAX="$(list_migrations_at_rev "$(base_ref)" | max_timestamp)"
    # Iterate each LOCAL migration; flag every one that's NOT on the base ref
    # AND has a timestamp <= main's newest. We can't trust `max(local)` because
    # if main already contained migration T2 and the dev creates T1 (smaller
    # than T2), `max(local)` would still be T2 — the check would pass while
    # the dev's actual T1 still breaks the apply order.
    if [[ -n "$MAIN_MAX" ]]; then
        # Build a pipe-delimited list of main migration timestamps for O(n)
        # substring lookups. Bash 3.2 (macOS default /bin/bash) has no
        # associative arrays, so we use the classic delimited-string set.
        # Format: "|TS1|TS2|...|" — bracket every value with pipes so a partial
        # prefix never matches (e.g. "|123|" does not match looking for "12").
        MAIN_TIMESTAMP_SET="|"
        while IFS= read -r m; do
            if [[ -n "$m" ]]; then
                MAIN_TIMESTAMP_SET+="$(migration_timestamp "$m")|"
            fi
        done < <(list_migrations_at_rev "$(base_ref)")

        while IFS= read -r local_m; do
            [[ -n "$local_m" ]] || continue
            local_ts="$(migration_timestamp "$local_m")"
            # Skip migrations that are also on main — those are common history,
            # not pending. Only verify the timestamps of pending migrations.
            if [[ "$MAIN_TIMESTAMP_SET" == *"|$local_ts|"* ]]; then
                continue
            fi
            if [[ "$local_ts" -le "$MAIN_MAX" ]]; then
                record_fail "Pending migration $local_m has timestamp $local_ts <= $(base_ref)'s newest ($MAIN_MAX)."
                record_fail "EF applies migrations in timestamp order; running this against an environment that already applied $MAIN_MAX would skip your migration silently."
                record_fail "Rebase + regenerate so your migration's timestamp lands after $MAIN_MAX:  ./tools/backend/migration/rebase.sh"
            fi
        done < <(list_local_migrations)
    fi
elif [[ "$MODE" == "full" ]]; then
    record_fail "Could not resolve $(base_ref) and we're in --full mode. Check network or set MIGRATION_BASE_REF."
else
    log_warn "Could not fetch $(base_ref); skipping cross-branch timestamp check."
fi

# (6) `ModelSnapshot.cs` must be staged whenever ANY migration `.cs` is staged.
# EF regenerates the snapshot file every time `migrations add` runs; forgetting
# to stage it is one of the most common "works on my machine" bugs because the
# dev's local DB has the right state but git's source-of-truth is out of date.
log_info "(6/6) Verifying ModelSnapshot.cs co-staging with migration changes..."
if git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    STAGED_MIGRATIONS="$(git -C "$REPO_ROOT" diff --cached --name-only -- "src/Backend/Infrastructure/Persistence/Migrations/[0-9]*.cs" 2>/dev/null || true)"
    STAGED_SNAPSHOT="$(git -C "$REPO_ROOT" diff --cached --name-only -- "src/Backend/Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs" 2>/dev/null || true)"
    if [[ -n "$STAGED_MIGRATIONS" && -z "$STAGED_SNAPSHOT" ]]; then
        record_fail "You staged migration files but ApplicationDbContextModelSnapshot.cs is not staged."
        record_fail "EF regenerates the snapshot on every \`migrations add\` — forgetting it leaves git's model state out of sync with the migration files."
        record_fail "Run:  git add src/Backend/Infrastructure/Persistence/Migrations/ApplicationDbContextModelSnapshot.cs"
    fi
fi

# ─────────────────────────────────────────────────────────────────────────────
# Full mode — dotnet & DB checks
# ─────────────────────────────────────────────────────────────────────────────

if [[ "$MODE" == "full" ]]; then
    log_step "Full-mode checks (build + DB apply + drift)"

    if ! command -v dotnet >/dev/null 2>&1; then
        record_fail ".NET SDK is required for --full mode but \`dotnet\` is not on PATH."
    else
        # We rebuild because EF tools spin up the host's assemblies; a stale
        # bin/ that doesn't reflect the latest model code would silently apply
        # the OLD model when checking drift.
        log_info "Restoring + building backend (this can take ~30s on a cold cache)..."
        if ! dotnet restore "$BACKEND_DIR" >"$RESTORE_LOG" 2>&1; then
            tail -40 "$RESTORE_LOG" >&2
            record_fail "\`dotnet restore\` failed. See $RESTORE_LOG for the full output."
        fi
        if ! dotnet build "$BACKEND_DIR" --no-restore -c Debug -v minimal >"$BUILD_LOG" 2>&1; then
            tail -40 "$BUILD_LOG" >&2
            record_fail "\`dotnet build\` failed. See $BUILD_LOG for the full output."
        fi

        # `migrations has-pending-model-changes` (EF Core 8+) returns non-zero
        # when the model code differs from ApplicationDbContextModelSnapshot.cs.
        # This is the single most reliable drift signal: it catches anything a
        # dev changed in entity code without running `migrations add`.
        log_info "Checking for unmigrated model changes (\`migrations has-pending-model-changes\`)..."
        if ! "${EF_INVOKE[@]}" migrations has-pending-model-changes --no-build >"$PENDING_LOG" 2>&1; then
            cat "$PENDING_LOG" >&2
            record_fail "Model code is ahead of the migration snapshot. Run:  ./tools/backend/migration/new.sh <Name>"
        fi

        # Finally, apply every migration to a fresh DB. We only do this when the
        # caller has explicitly provided a sandbox connection string — applying
        # to a developer's real local DB would erase their working data.
        if [[ -n "${DRIFT_CHECK_CONNECTION:-}" ]]; then
            log_info "Applying full migration history to sandbox DB..."
            export ConnectionStrings__DatabaseConnectionString="$DRIFT_CHECK_CONNECTION"
            export ConnectionStrings__DatabaseReadConnectionString="$DRIFT_CHECK_CONNECTION"
            if ! "${EF_INVOKE[@]}" database update --no-build >"$APPLY_LOG" 2>&1; then
                tail -60 "$APPLY_LOG" >&2
                record_fail "\`dotnet ef database update\` failed against DRIFT_CHECK_CONNECTION (see log)."
            fi
        else
            log_dim "Set DRIFT_CHECK_CONNECTION to a sandbox Postgres URL to enable the full-apply check."
        fi
    fi
fi

# ─────────────────────────────────────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────────────────────────────────────

echo
if [[ "$FAIL_COUNT" -eq 0 ]]; then
    log_ok "All migration checks passed."
    exit 0
fi
log_error "Drift logs available at: $DRIFT_LOG_DIR"
log_error "Migration drift check failed with $FAIL_COUNT issue(s) above."
log_error "Run \`./tools/backend/migration/rebase.sh\` to walk through the safe fix flow,"
log_error "or see docs/backend/MIGRATION_WORKFLOW.md for troubleshooting."
exit 1
