#!/usr/bin/env bash
# Roll back application containers to a previous IMAGE_TAG (no DB migration).
# Usage: IMAGE_TAG=<tag> ./deploy/scripts/rollback.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

ENV_NAME="${1:-staging}"
shift || true

if [[ -z "${IMAGE_TAG:-}" ]]; then
  echo "IMAGE_TAG is required (previous release tag or BUILD_NUMBER)." >&2
  exit 1
fi

export SKIP_MIGRATE=1

echo "Rolling back ${ENV_NAME} to IMAGE_TAG=${IMAGE_TAG} (no migrate)..."
"${SCRIPT_DIR}/deploy-app.sh" "${ENV_NAME}" "$@"
"${SCRIPT_DIR}/smoke.sh" "${ENV_NAME}"
