#!/usr/bin/env bash
# Run EF migrations in a one-shot container. Usage: ./deploy/scripts/migrate.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"

echo "Applying database migrations ($ENV_NAME)..."
docker network create finalproject-app 2>/dev/null || true
compose_stack "$ENV_NAME" run --rm migrate
