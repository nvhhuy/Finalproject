#!/usr/bin/env bash
# Start / update application stack. Usage: ./deploy/scripts/up.sh [staging|production] [compose args...]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
shift || true

if [[ -n "${HARBOR_USER:-}" && -n "${HARBOR_PASSWORD:-}" ]]; then
  "${SCRIPT_DIR}/harbor-login.sh"
fi

echo "Starting stack ($ENV_NAME)..."
docker network create baseproject-app 2>/dev/null || true
compose_stack "$ENV_NAME" up -d --pull always "$@"
