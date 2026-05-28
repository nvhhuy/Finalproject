#!/usr/bin/env bash
# Build all application images. Usage: ./deploy/scripts/build.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
shift || true

echo "Building images for environment: $ENV_NAME"
compose_stack "$ENV_NAME" build "$@"
