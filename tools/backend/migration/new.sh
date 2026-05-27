#!/usr/bin/env bash
# Safe wrapper around `dotnet ef migrations add` that runs preflight checks
# BEFORE the migration is generated, instead of having the dev discover hours
# later that they branched off stale main:
#
#   1. Working tree must be clean (otherwise unrelated edits get mixed into the
#      migration commit).
#   2. The branch must be up to date with origin/main (or the dev acknowledges
#      they want to create on top of stale main).
#   3. `dotnet ef migrations has-pending-model-changes` must report changes —
#      otherwise creating a migration is a no-op that produces an empty file.
#
# After generation we run the fast drift check so the dev sees any timestamp /
# snapshot issues immediately, without waiting for CI feedback.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/lib/common.sh"

usage() {
    cat <<'EOF'
Usage: new.sh <MigrationName> [--allow-stale]

  <MigrationName>   PascalCase name describing what the migration does, e.g.
                    AddMediaAssetTags. Becomes part of the file name AND the
                    __EFMigrationsHistory row, so make it descriptive.

  --allow-stale     Skip the "up-to-date with origin/main" check. Use only
                    when intentionally branching off an older commit.

Env vars:
  MIGRATION_BASE_REF   Trunk branch. Defaults to origin/main.
EOF
}

if [[ $# -lt 1 ]]; then
    usage
    exit 2
fi

# Two-pass arg parsing: flags first (so `--help` always wins, regardless of
# position), then validate the single required positional argument. This lets
# devs type `new.sh --help` without being told their migration name is invalid.
NAME=""
ALLOW_STALE=0
for arg in "$@"; do
    case "$arg" in
        -h|--help) usage; exit 0 ;;
        --allow-stale) ALLOW_STALE=1 ;;
        --*|-*) log_error "Unknown flag: $arg"; usage; exit 2 ;;
        *)
            if [[ -n "$NAME" ]]; then
                log_error "Only one migration name allowed (got '$NAME' and '$arg')."
                exit 2
            fi
            NAME="$arg"
            ;;
    esac
done

if [[ -z "$NAME" ]]; then
    log_error "Migration name is required."
    usage
    exit 2
fi

if [[ ! "$NAME" =~ ^[A-Z][A-Za-z0-9_]*$ ]]; then
    log_error "Migration name '$NAME' is not valid PascalCase identifier."
    log_error "Expected: starts with uppercase letter, then letters/digits/underscores only."
    log_error "Examples:  AddMediaAssetTags  •  AlterUserNormalizedEmailIndex  •  DropLegacyAuditLog"
    exit 2
fi

cd "$REPO_ROOT"

log_step "Preflight"

if ! command -v dotnet >/dev/null 2>&1; then
    log_error ".NET SDK required but \`dotnet\` not on PATH."
    exit 2
fi

if ! dotnet ef --version >/dev/null 2>&1; then
    log_error "\`dotnet ef\` not installed. Install with: dotnet tool install --global dotnet-ef"
    exit 2
fi

if [[ "$(git_is_dirty)" == "1" ]]; then
    log_error "Working tree has uncommitted changes:"
    git -C "$REPO_ROOT" status --short --untracked-files=no >&2
    log_error "Commit or stash them first so the migration commit is clean."
    exit 1
fi

# Check up-to-date-ness against origin/main. This catches the common case where
# a dev pulled main two days ago, then their teammates merged migrations in the
# meantime, and the dev would have created a stale-timestamp migration without
# noticing. We only warn (not error) if the dev passed --allow-stale.
if ensure_base_ref; then
    LOCAL_MAX="$(list_local_migrations | max_timestamp || true)"
    MAIN_MAX="$(list_migrations_at_rev "$(base_ref)" | max_timestamp || true)"
    if [[ -n "$LOCAL_MAX" && -n "$MAIN_MAX" && "$LOCAL_MAX" -lt "$MAIN_MAX" ]]; then
        if [[ "$ALLOW_STALE" -eq 1 ]]; then
            log_warn "Your branch is behind $(base_ref) (you: $LOCAL_MAX, main: $MAIN_MAX) but --allow-stale was given. Proceeding."
        else
            log_error "Your branch's newest migration ($LOCAL_MAX) is older than $(base_ref)'s newest ($MAIN_MAX)."
            log_error "Creating a migration now would produce a stale timestamp + snapshot."
            log_error "Either rebase first:        git fetch && git rebase $(base_ref)"
            log_error "Or accept the risk:         re-run with --allow-stale"
            exit 1
        fi
    fi
else
    log_warn "Could not fetch $(base_ref); skipping the up-to-date check."
fi

# `has-pending-model-changes` (EF Core 8+) exits non-zero when entity code has
# diverged from the snapshot — i.e. there IS something to migrate. We invert
# this so the dev sees a friendlier message instead of "Build succeeded. ✓" with
# zero diff.
log_info "Verifying entity code has changes to migrate..."
if "${EF_INVOKE[@]}" migrations has-pending-model-changes >/dev/null 2>&1; then
    log_warn "No pending model changes detected. Are you sure you need a migration?"
    log_warn "Continuing anyway in case you're regenerating an empty placeholder."
fi

log_step "Generating migration: $NAME"

# Add the migration. EF prints the file paths it created; we don't capture
# them because the dev usually wants to see the output (compile errors, etc.).
if ! "${EF_INVOKE[@]}" migrations add "$NAME"; then
    log_error "\`dotnet ef migrations add\` failed. See output above."
    exit 1
fi

log_step "Post-checks"

# Run the fast drift detector immediately so any silly mistakes (forgot to
# rebase, weird timestamp, etc.) surface here instead of from CI in 10 minutes.
if ! "$SCRIPT_DIR/check.sh" --fast; then
    log_error "Migration created but drift check failed. See messages above."
    exit 1
fi

log_step "Done"

log_ok "Migration $NAME created on top of $(base_ref)."
echo
log_info "Next steps:"
echo "  1. Review the generated SQL:  cat $MIGRATIONS_DIR/*${NAME}.cs"
echo "  2. Apply locally:             ./tools/backend/migrate.sh"
echo "  3. Stage + commit:            git add $MIGRATIONS_DIR/ && git commit -m \"migration: $NAME\""
echo
