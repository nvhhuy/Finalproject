#!/usr/bin/env bash
# Apply application stack: optional migrate, then up with image pull.
# Usage: ./deploy/scripts/deploy-app.sh [staging|production]
# Env:
#   SKIP_MIGRATE=1  — skip DB migration (rollback)
#   IMAGE_TAG       — override tag from env files
#   HARBOR_USER/HARBOR_PASSWORD — optional registry login before pull
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
shift || true

SKIP_MIGRATE="${SKIP_MIGRATE:-0}"

if [[ -n "${HARBOR_USER:-}" && -n "${HARBOR_PASSWORD:-}" ]]; then
  "${SCRIPT_DIR}/harbor-login.sh"
fi

docker network create baseproject-app 2>/dev/null || true

if [[ "${SKIP_MIGRATE}" != "1" ]]; then
  echo "Applying database migrations (${ENV_NAME})..."
  compose_stack "${ENV_NAME}" --profile tools run --rm migrate
else
  echo "Skipping database migration (SKIP_MIGRATE=1)."
fi

echo "Starting stack (${ENV_NAME}) with --pull always..."
compose_stack "${ENV_NAME}" up -d --pull always "$@"
