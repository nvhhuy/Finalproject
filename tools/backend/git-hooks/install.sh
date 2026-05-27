#!/usr/bin/env bash
# Wire the repo's tracked hooks directory into git via `core.hooksPath`.
# Run once after cloning:  ./tools/backend/git-hooks/install.sh
#
# Why `core.hooksPath` instead of copying into `.git/hooks/`?
#   • Hooks live alongside the code they protect, version-controlled, reviewed.
#   • Updating a hook is just `git pull` — no out-of-band sync required.
#   • Removing local copies in `.git/hooks/` removes the temptation to edit
#     them by hand and forget to share the fix.
#
# Requires Git 2.9+ (released July 2016, ubiquitous). Older Git versions will
# need a symlink fallback; we don't support them.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
if [[ -z "$REPO_ROOT" ]]; then
    echo "Run from inside a git checkout." >&2
    exit 1
fi

HOOKS_DIR_RELATIVE="tools/backend/git-hooks"
HOOKS_DIR_ABS="$REPO_ROOT/$HOOKS_DIR_RELATIVE"

if [[ ! -d "$HOOKS_DIR_ABS" ]]; then
    echo "Hooks directory not found: $HOOKS_DIR_ABS" >&2
    exit 1
fi

# Sanity-check Git version. We use `git config --type=path` (added 2.18) just
# for nicer output; the actual `core.hooksPath` feature is 2.9.
GIT_VERSION="$(git --version | awk '{print $3}')"
MAJOR="${GIT_VERSION%%.*}"; REST="${GIT_VERSION#*.}"; MINOR="${REST%%.*}"
if (( MAJOR < 2 || (MAJOR == 2 && MINOR < 9) )); then
    echo "Git $GIT_VERSION is too old; need 2.9 or newer for core.hooksPath support." >&2
    exit 1
fi

# Make sure every hook script in the tracked directory is executable. Git
# enforces this and a freshly-cloned repo may have lost the +x bit on some
# filesystems (cross-platform clones, zip archives, etc.).
find "$HOOKS_DIR_ABS" -maxdepth 1 -type f ! -name '*.sh' -exec chmod +x {} \;
find "$HOOKS_DIR_ABS" -maxdepth 1 -type f -name '*.sh' -exec chmod +x {} \;

# Point Git at the tracked directory. We use the path RELATIVE to the repo root
# so the same config works on every clone (an absolute path would only work for
# this specific developer's checkout location).
git -C "$REPO_ROOT" config core.hooksPath "$HOOKS_DIR_RELATIVE"

echo "✓ core.hooksPath set to $HOOKS_DIR_RELATIVE"
echo
echo "Active hooks:"
find "$HOOKS_DIR_ABS" -maxdepth 1 -type f -perm -u+x ! -name '*.sh' -exec basename {} \;
echo
echo "To uninstall:  git config --unset core.hooksPath"
