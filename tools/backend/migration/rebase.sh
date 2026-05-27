#!/usr/bin/env bash
# Interactive migration rebase. Solves the "I branched off main last week, my
# teammates merged migrations T2/T3 in the meantime, and now my migration T1 is
# out of order" problem by walking the dev through the only reliable fix:
#
#   1. Revert the local DB to whatever migration is the last common ancestor
#      with origin/main (so EF allows `migrations remove` to delete the pending
#      ones).
#   2. Remove the dev's pending migrations from the codebase via `dotnet ef
#      migrations remove` (each call removes the topmost migration; we loop).
#   3. Rebase the git branch onto origin/main so the model code now reflects
#      the merged state PLUS the dev's own changes.
#   4. Re-create the migrations with `dotnet ef migrations add`. The new files
#      carry fresh timestamps after origin/main's tip and a snapshot that
#      includes every change merged in the meantime.
#
# The script is deliberately conservative: every destructive step asks for
# confirmation, and any failure aborts the run with a hint on how to recover
# manually. It does NOT auto-amend the git commit because that destroys the
# dev's history without warning — instead it prints the exact commands to run
# at the end.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib/common.sh"

ASSUME_YES=0
SKIP_REBASE=0
for arg in "$@"; do
    case "$arg" in
        -y|--yes) ASSUME_YES=1 ;;
        --skip-rebase) SKIP_REBASE=1 ;;
        -h|--help)
            cat <<'EOF'
Usage: rebase.sh [--yes] [--skip-rebase]

Interactive helper that regenerates your pending migrations on top of the
current origin/main. Walks through:
  1. revert local DB to the last shared migration
  2. `dotnet ef migrations remove` (×N)
  3. `git rebase <base>`  (skip with --skip-rebase if you've already rebased)
  4. `dotnet ef migrations add <Name>` (×N) — you'll be prompted for each name

Options:
  -y, --yes       Don't prompt for confirmation before destructive steps.
                  Use only inside automation; in a TTY you almost always want
                  to read the diff before answering.
  --skip-rebase   Skip the `git rebase` step. Useful when you've already
                  rebased manually and just need to remove + regenerate.

Env vars:
  MIGRATION_BASE_REF   Trunk branch. Defaults to origin/main.
EOF
            exit 0
            ;;
        *)
            log_error "Unknown argument: $arg"
            exit 2
            ;;
    esac
done

cd "$REPO_ROOT"

confirm() {
    # Helper: prints prompt, returns 0 on yes, 1 on no. Treats --yes as
    # auto-confirm so CI / scripted runs can pass through unattended.
    local prompt="$1"
    if [[ "$ASSUME_YES" -eq 1 ]]; then
        log_dim "[--yes] $prompt → assumed yes"
        return 0
    fi
    local reply
    read -r -p "$prompt [y/N] " reply
    [[ "$reply" =~ ^[Yy]$ ]]
}

bail() {
    log_error "$1"
    log_error "Aborting. Your repo and DB are in the state they were before the last step."
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 0: Preconditions
# ─────────────────────────────────────────────────────────────────────────────

log_step "Preflight checks"

if ! command -v dotnet >/dev/null 2>&1; then
    bail ".NET SDK is required but \`dotnet\` is not on PATH."
fi

if ! dotnet ef --version >/dev/null 2>&1; then
    bail "\`dotnet ef\` is not installed. Install with: dotnet tool install --global dotnet-ef"
fi

if [[ "$(git_is_dirty)" == "1" ]]; then
    log_error "Working tree has uncommitted changes (excluding untracked files):"
    git -C "$REPO_ROOT" status --short --untracked-files=no >&2
    bail "Commit or stash your changes before running rebase.sh — otherwise \`git rebase\` will refuse to run and the migration regen step would mix unrelated edits into the new migration."
fi

CURRENT_BRANCH="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)"
case "$CURRENT_BRANCH" in
    main|master)
        bail "You're on '$CURRENT_BRANCH'. Switch to your feature branch first."
        ;;
    HEAD)
        bail "You're in a detached HEAD state. Check out the feature branch you want to rebase."
        ;;
esac

if ! ensure_base_ref; then
    bail "Cannot resolve $(base_ref). Check network connectivity or set MIGRATION_BASE_REF."
fi

log_ok "Branch=$CURRENT_BRANCH | Base=$(base_ref)"

# ─────────────────────────────────────────────────────────────────────────────
# Step 1: Identify pending migrations
# ─────────────────────────────────────────────────────────────────────────────

log_step "Identifying pending migrations"

# Read migrations into arrays the bash 3.2 way (no `mapfile`). Using a
# while-read loop with a process substitution preserves ordering without
# tripping over migration names that contain spaces (they can't, by EF's
# rules, but defensive coding).
MAIN_MIGRATIONS=()
while IFS= read -r line; do
    [[ -n "$line" ]] && MAIN_MIGRATIONS+=("$line")
done < <(list_migrations_at_rev "$(base_ref)")

LOCAL_MIGRATIONS=()
while IFS= read -r line; do
    [[ -n "$line" ]] && LOCAL_MIGRATIONS+=("$line")
done < <(list_local_migrations)

# Anything in LOCAL_MIGRATIONS but NOT in MAIN_MIGRATIONS is "pending" — that
# is, introduced on this branch. We diff by exact migration identifier (full
# `<timestamp>_<name>`) so a migration that was renamed on origin/main still
# counts as different from the local one with the old name.
#
# Bash 3.2 has no associative arrays, so we use a delimited-string set with
# pipes around every value to prevent prefix collisions.
MAIN_SET="|"
for m in "${MAIN_MIGRATIONS[@]:-}"; do
    [[ -n "$m" ]] && MAIN_SET+="$m|"
done

PENDING=()
for m in "${LOCAL_MIGRATIONS[@]:-}"; do
    [[ -n "$m" ]] || continue
    if [[ "$MAIN_SET" != *"|$m|"* ]]; then
        PENDING+=("$m")
    fi
done

if [[ "${#PENDING[@]}" -eq 0 ]]; then
    log_ok "No pending migrations relative to $(base_ref). Nothing to rebase."
    exit 0
fi

# Find the most recent migration that exists on BOTH branches — that's our
# rollback target. EF Core requires us to revert past every migration we want
# to remove, so this is the deepest point we'll touch in the DB.
LAST_COMMON=""
for m in "${LOCAL_MIGRATIONS[@]:-}"; do
    [[ -n "$m" ]] || continue
    if [[ "$MAIN_SET" == *"|$m|"* ]]; then
        LAST_COMMON="$m"
    fi
done
if [[ -z "$LAST_COMMON" ]]; then
    LAST_COMMON="0"  # EF's sentinel for "no migration applied" — revert to empty DB
fi

log_info "Pending migrations on this branch (will be regenerated):"
for m in "${PENDING[@]}"; do
    printf '  %s\n' "$m"
done
log_info "Last migration shared with $(base_ref): ${LAST_COMMON}"

echo
log_warn "This script will:"
echo "  1. Revert your LOCAL Postgres database back to ${LAST_COMMON}"
echo "     (any data inserted after that migration will be wiped)"
echo "  2. Delete the pending migration files above from disk"
echo "  3. ${SKIP_REBASE:+(skipped) }Rebase your branch onto $(base_ref)"
echo "  4. Re-create the migrations — you'll be prompted for each name"
echo

if ! confirm "Proceed?"; then
    log_info "Aborted by user."
    exit 0
fi

# ─────────────────────────────────────────────────────────────────────────────
# Step 2: Revert local DB
# ─────────────────────────────────────────────────────────────────────────────

log_step "Reverting local DB to ${LAST_COMMON}"

# `dotnet ef database update <target>` rolls forward OR backward, depending on
# where __EFMigrationsHistory currently sits. We invoke it explicitly so the
# next `migrations remove` step finds the snapshot in sync with the DB.
if ! "${EF_INVOKE[@]}" database update "$LAST_COMMON"; then
    bail "Failed to revert DB to ${LAST_COMMON}. If your DB is in a bad state, you can drop and re-create it: dropdb finalproject && createdb finalproject"
fi
log_ok "DB reverted."

# ─────────────────────────────────────────────────────────────────────────────
# Step 3: Remove pending migrations
# ─────────────────────────────────────────────────────────────────────────────

log_step "Removing pending migrations (${#PENDING[@]} total)"

# `migrations remove` only removes the LAST migration each time, so we loop
# from newest to oldest. We don't try to be clever about partial failures —
# any non-zero exit aborts and the dev can re-run.
PENDING_REVERSE=()
for ((i=${#PENDING[@]}-1; i>=0; i--)); do
    PENDING_REVERSE+=("${PENDING[i]}")
done

REMOVED_NAMES=()
for m in "${PENDING_REVERSE[@]}"; do
    NAME="${m#*_}"  # strip the timestamp prefix to recover just the migration name
    REMOVED_NAMES=("$NAME" "${REMOVED_NAMES[@]}")  # preserve original order
    log_info "Removing ${m}..."
    if ! "${EF_INVOKE[@]}" migrations remove --force; then
        bail "Failed to remove ${m}. Run \`dotnet ef migrations remove --force\` manually and inspect the error."
    fi
done
log_ok "All pending migrations removed."

# ─────────────────────────────────────────────────────────────────────────────
# Step 4: Rebase
# ─────────────────────────────────────────────────────────────────────────────

if [[ "$SKIP_REBASE" -eq 0 ]]; then
    log_step "Rebasing onto $(base_ref)"

    # We staged the migration-removal as uncommitted changes, so before
    # running `git rebase` we have to either commit or stash them. Stash is
    # safer — if rebase fails we can drop the stash and try again.
    git -C "$REPO_ROOT" stash push --include-untracked \
        --message "migration-rebase.sh: pending migration removals" >/dev/null

    if git -C "$REPO_ROOT" rebase "$(base_ref)"; then
        log_ok "Rebase clean."
    else
        log_error "Rebase produced conflicts. Resolve them, then run:"
        log_error "    git rebase --continue"
        log_error "    git stash pop"
        log_error "    ./tools/backend/migration/rebase.sh --skip-rebase"
        exit 1
    fi

    if ! git -C "$REPO_ROOT" stash pop; then
        bail "Failed to restore stashed migration removals. Run \`git stash pop\` manually."
    fi
else
    log_step "Skipping rebase (--skip-rebase)"
fi

# ─────────────────────────────────────────────────────────────────────────────
# Step 5: Re-create migrations
# ─────────────────────────────────────────────────────────────────────────────

log_step "Re-creating migrations"

# Smart default: if there was exactly one pending migration, re-use its name
# without asking. Multiple pending migrations almost always need a single
# consolidated migration after rebase (because the model code is now in one
# final state) so we ask the dev whether to consolidate or split.
if [[ "${#REMOVED_NAMES[@]}" -eq 1 ]]; then
    NEW_NAME="${REMOVED_NAMES[0]}"
    log_info "Re-creating migration with original name: ${NEW_NAME}"
    if ! "${EF_INVOKE[@]}" migrations add "$NEW_NAME"; then
        bail "Failed to re-create migration. Check the build output above."
    fi
    log_ok "Migration ${NEW_NAME} regenerated."
else
    log_warn "You had ${#REMOVED_NAMES[@]} pending migrations:"
    for n in "${REMOVED_NAMES[@]}"; do printf '  %s\n' "$n"; done
    echo
    log_warn "Re-creating them as one consolidated migration is the safe default."
    log_warn "If you need to preserve the logical split, you'll have to do it"
    log_warn "manually: revert specific entity changes, add migration, restore"
    log_warn "the next change, add migration, etc."
    echo
    if ! confirm "Consolidate into a single migration?"; then
        log_info "Aborted. Your DB is at $LAST_COMMON; entity code is rebased; no migration files exist."
        log_info "Run \`dotnet ef migrations add <Name>\` yourself, splitting as needed."
        exit 0
    fi

    DEFAULT_NAME="$(printf '%s' "${REMOVED_NAMES[*]}" | tr ' ' '_')"
    if [[ -z "$DEFAULT_NAME" ]]; then DEFAULT_NAME="RebasedChanges"; fi

    if [[ "$ASSUME_YES" -eq 1 ]]; then
        NEW_NAME="$DEFAULT_NAME"
    else
        read -r -p "Name for the consolidated migration [$DEFAULT_NAME]: " NEW_NAME
        NEW_NAME="${NEW_NAME:-$DEFAULT_NAME}"
    fi
    if ! "${EF_INVOKE[@]}" migrations add "$NEW_NAME"; then
        bail "Failed to create consolidated migration. Check the build output above."
    fi
    log_ok "Consolidated migration ${NEW_NAME} created."
fi

# ─────────────────────────────────────────────────────────────────────────────
# Step 6: Final guidance
# ─────────────────────────────────────────────────────────────────────────────

log_step "Done"

log_ok "All migrations regenerated on top of $(base_ref)."
echo
log_info "Recommended next steps:"
echo
echo "  1. Apply the new migration locally to confirm it runs cleanly:"
echo "       ./tools/backend/migrate.sh"
echo
echo "  2. Run the fast drift check:"
echo "       ./tools/backend/migration/check.sh"
echo
echo "  3. Stage + amend / commit the regenerated migration files:"
echo "       git add src/Backend/Infrastructure/Persistence/Migrations/"
echo "       git commit --amend --no-edit     # or: git commit -m '...'"
echo
echo "  4. Push your branch (force-with-lease since rebase rewrote history):"
echo "       git push --force-with-lease origin $CURRENT_BRANCH"
echo
