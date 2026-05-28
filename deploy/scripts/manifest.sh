#!/usr/bin/env bash
# Record deployment metadata for audit/rollback trail.
# Usage:
#   ./deploy/scripts/manifest.sh [staging|production] [status]
# Example:
#   ./deploy/scripts/manifest.sh production success
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
STATUS="${2:-success}"
COMMON="$DEPLOY/env/shared/app.common.env"
TIMESTAMP="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
USER_NAME="${SUDO_USER:-${USER:-unknown}}"
HOST_NAME="$(hostname)"
SHA="$(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)"
BRANCH="$(git -C "$ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
IMAGE_REGISTRY_VALUE="${IMAGE_REGISTRY:-unknown}"
IMAGE_TAG_VALUE="${IMAGE_TAG:-unknown}"
LOG_DIR="$(release_logs_dir "$ENV_NAME")"
LOG_FILE="$LOG_DIR/deploy.log"

APP_HOST_VALUE=""
ADMIN_HOST_VALUE=""
if [[ -f "$COMMON" ]]; then
  APP_HOST_VALUE="$(awk -F= '$1=="APP_HOST"{print $2}' "$COMMON" | tr -d '\r')"
  ADMIN_HOST_VALUE="$(awk -F= '$1=="ADMIN_HOST"{print $2}' "$COMMON" | tr -d '\r')"
fi

printf '{"time":"%s","env":"%s","status":"%s","user":"%s","host":"%s","branch":"%s","sha":"%s","image_registry":"%s","image_tag":"%s","app_host":"%s","admin_host":"%s"}\n' \
  "$TIMESTAMP" "$ENV_NAME" "$STATUS" "$USER_NAME" "$HOST_NAME" "$BRANCH" "$SHA" "$IMAGE_REGISTRY_VALUE" "$IMAGE_TAG_VALUE" "$APP_HOST_VALUE" "$ADMIN_HOST_VALUE" \
  >> "$LOG_FILE"

echo "Deployment manifest appended: $LOG_FILE"
