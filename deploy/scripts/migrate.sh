#!/usr/bin/env bash
# Run EF migrations in a one-shot container. Usage: ./deploy/scripts/migrate.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"

echo "Applying database migrations ($ENV_NAME)..."
docker network create baseproject-app 2>/dev/null || true

if [[ -n "${HARBOR_USER:-}" && -n "${HARBOR_PASSWORD:-}" ]]; then
  "${SCRIPT_DIR}/harbor-login.sh"
fi

compose_stack "$ENV_NAME" --profile tools run --rm migrate
