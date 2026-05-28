#!/usr/bin/env bash
# Stop infrastructure stack. Usage:
#   ./deploy/scripts/infra-down.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"

echo "Stopping infra stack ($ENV_NAME)..."
compose_infra "$ENV_NAME" down
