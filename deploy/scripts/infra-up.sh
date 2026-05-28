#!/usr/bin/env bash
# Start/update infrastructure stack. Usage:
#   ./deploy/scripts/infra-up.sh [staging|production] [compose args...]
# Examples:
#   ./deploy/scripts/infra-up.sh staging
#   ./deploy/scripts/infra-up.sh staging --profile observability --profile monitoring
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
shift || true
COMPOSE_ARGS=("$@")

docker network create baseproject-app 2>/dev/null || true
docker network create proxy 2>/dev/null || true
echo "Starting infra stack ($ENV_NAME)..."
compose_infra "$ENV_NAME" "${COMPOSE_ARGS[@]}" up -d
