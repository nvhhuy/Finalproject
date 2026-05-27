#!/usr/bin/env bash
# Shared helpers for the migration toolchain. Source this file from sibling scripts:
#   source "$(dirname "$0")/lib/common.sh"
#
# Conventions:
#   - All paths resolved from REPO_ROOT (computed once at source time).
#   - Functions never `exit`; they print to stderr and return non-zero so callers
#     can compose them and decide error handling.
#   - Colour output goes through `cstyle` so non-TTY contexts (CI, file redirection)
#     get plain text automatically.

set -o pipefail

# ─────────────────────────────────────────────────────────────────────────────
# Paths
# ─────────────────────────────────────────────────────────────────────────────

# Resolve the repo root once. This file lives at
# `tools/backend/migration/lib/common.sh`, so the repo root is four levels up.
# Variables below are exported for use by scripts that source this file.
# ShellCheck can't see across `source` boundaries so SC2034 warnings on
# cross-script consumers are false positives — silenced inline.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
BACKEND_DIR="$REPO_ROOT/src/Backend"
MIGRATIONS_DIR="$BACKEND_DIR/Infrastructure/Persistence/Migrations"
# shellcheck disable=SC2034  # used by check.sh
MODEL_SNAPSHOT_FILE="$MIGRATIONS_DIR/ApplicationDbContextModelSnapshot.cs"

# Default `dotnet ef` invocation for this repo. Centralised so all scripts use
# the same project / startup-project / context triple — changing the layout is
# a one-line edit here instead of grepping every script.
# shellcheck disable=SC2034  # used by rebase.sh, new.sh, check.sh
EF_INVOKE=(dotnet ef
    --project "$BACKEND_DIR/Infrastructure/Infrastructure.csproj"
    --startup-project "$BACKEND_DIR/Api/Api.csproj"
    --context ApplicationDbContext)

# ─────────────────────────────────────────────────────────────────────────────
# Logging
# ─────────────────────────────────────────────────────────────────────────────

# Colour helpers — disabled when stdout is not a TTY (CI, piping, etc.) so log
# files don't fill up with ANSI escape codes.
if [[ -t 1 ]]; then
    _C_RESET=$'\033[0m'; _C_BOLD=$'\033[1m'; _C_DIM=$'\033[2m'
    _C_RED=$'\033[31m'; _C_GREEN=$'\033[32m'; _C_YELLOW=$'\033[33m'; _C_BLUE=$'\033[34m'
else
    _C_RESET=; _C_BOLD=; _C_DIM=; _C_RED=; _C_GREEN=; _C_YELLOW=; _C_BLUE=
fi

log_info()  { printf '%s[info]%s  %s\n' "$_C_BLUE"   "$_C_RESET" "$*"; }
log_ok()    { printf '%s[ok]%s    %s\n' "$_C_GREEN"  "$_C_RESET" "$*"; }
log_warn()  { printf '%s[warn]%s  %s\n' "$_C_YELLOW" "$_C_RESET" "$*" >&2; }
log_error() { printf '%s[error]%s %s\n' "$_C_RED"    "$_C_RESET" "$*" >&2; }
log_step()  { printf '\n%s── %s ──%s\n' "$_C_BOLD"   "$*"        "$_C_RESET"; }
log_dim()   { printf '%s%s%s\n'         "$_C_DIM"    "$*"        "$_C_RESET"; }

# ─────────────────────────────────────────────────────────────────────────────
# Migration file discovery
# ─────────────────────────────────────────────────────────────────────────────

# Print the migration file names (without `.Designer.cs` suffix) sorted by their
# timestamp prefix. Each output line is one migration identifier such as
# `20260512143823_BaseFoundation`.
list_local_migrations() {
    if [[ ! -d "$MIGRATIONS_DIR" ]]; then
        return 0
    fi
    # The `.Designer.cs` files mirror the migration `.cs` files; we deduplicate
    # by stripping the suffix so each migration appears exactly once.
    find "$MIGRATIONS_DIR" -maxdepth 1 -type f -name '[0-9]*.cs' \
        | sed -E 's#.*/##; s#\.Designer\.cs$##; s#\.cs$##' \
        | sort -u
}

# Same as list_local_migrations but for whatever the given git revision contains.
# Useful for diffing the dev's local tree against `origin/main`.
list_migrations_at_rev() {
    local rev="$1"
    # `git ls-tree` is the lowest-level way to enumerate a path at a revision
    # without checking it out. Output is `<mode> <type> <hash>\t<path>`; we just
    # need the basename.
    if ! git -C "$REPO_ROOT" rev-parse "$rev" >/dev/null 2>&1; then
        return 0
    fi
    git -C "$REPO_ROOT" ls-tree -r --name-only "$rev" -- \
        "src/Backend/Infrastructure/Persistence/Migrations" 2>/dev/null \
        | awk -F'/' '{print $NF}' \
        | grep -E '^[0-9]+_.+\.cs$' \
        | sed -E 's#\.Designer\.cs$##; s#\.cs$##' \
        | sort -u
}

# Extract just the timestamp prefix from a migration identifier.
migration_timestamp() {
    local name="$1"
    printf '%s\n' "${name%%_*}"
}

# Print the highest timestamp from a list of migration identifiers piped in via
# stdin. Returns empty if no migrations exist.
max_timestamp() {
    awk -F'_' '{print $1}' | sort -n | tail -1
}

# ─────────────────────────────────────────────────────────────────────────────
# Git helpers
# ─────────────────────────────────────────────────────────────────────────────

# Echo "1" if the working tree (incl. index) is dirty, otherwise "0". We treat
# any staged or unstaged change as dirty, but ignore untracked files because
# they don't affect migration safety.
git_is_dirty() {
    if [[ -n "$(git -C "$REPO_ROOT" status --porcelain=v1 --untracked-files=no)" ]]; then
        printf '1\n'
    else
        printf '0\n'
    fi
}

# Resolve the configured upstream main branch. Allows operators to override the
# default `origin/main` via the MIGRATION_BASE_REF env var (e.g. for forks that
# keep the trunk on `origin/develop`).
base_ref() {
    printf '%s\n' "${MIGRATION_BASE_REF:-origin/main}"
}

# Ensure the base ref exists; try to fetch silently when we're online. Returns
# non-zero only when the ref truly cannot be resolved (e.g. detached air-gapped
# clone) so callers can warn-and-continue instead of hard-failing.
ensure_base_ref() {
    local ref
    ref="$(base_ref)"
    if git -C "$REPO_ROOT" rev-parse "$ref" >/dev/null 2>&1; then
        return 0
    fi
    log_dim "Fetching $ref so the drift check has something to compare against..."
    if git -C "$REPO_ROOT" fetch --quiet "${ref%%/*}" "${ref#*/}" 2>/dev/null; then
        return 0
    fi
    return 1
}
