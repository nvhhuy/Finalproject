#!/usr/bin/env bash
# Deploy on the target VPS over SSH.
# Usage: deploy-remote.sh <staging|production> <deploy_host> <repo_path>
#
# Env (from Jenkins or operator shell):
#   IMAGE_REGISTRY, IMAGE_TAG — required
#   SKIP_MIGRATE=1            — rollback (no DB migration)
#   HARBOR_USER/PASSWORD      — optional; docker login before pull
#   RUN_VPS_SMOKE=1           — run deploy/scripts/smoke.sh after up (default 1)
set -euo pipefail

ENV_NAME="${1:?environment required (staging|production)}"
DEPLOY_HOST="${2:?deploy host required}"
DEPLOY_PATH="${3:?repo path on server required}"

: "${IMAGE_REGISTRY:?IMAGE_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"
: "${DEPLOY_SSH_USER:?DEPLOY_SSH_USER is required}"

SKIP_MIGRATE="${SKIP_MIGRATE:-0}"
RUN_VPS_SMOKE="${RUN_VPS_SMOKE:-1}"
REMOTE="${DEPLOY_SSH_USER}@${DEPLOY_HOST}"

if [[ "${SKIP_MIGRATE}" == "1" ]]; then
  echo "Rolling back ${ENV_NAME} on ${REMOTE}:${DEPLOY_PATH} tag=${IMAGE_TAG} (skip migrate)..."
else
  echo "Deploying ${ENV_NAME} on ${REMOTE}:${DEPLOY_PATH} tag=${IMAGE_TAG}..."
fi

ssh -o StrictHostKeyChecking=accept-new "${REMOTE}" bash -s <<EOF
set -euo pipefail
cd "${DEPLOY_PATH}"
export SKIP_MIGRATE="${SKIP_MIGRATE}"
export RUN_VPS_SMOKE="${RUN_VPS_SMOKE}"
export IMAGE_REGISTRY="${IMAGE_REGISTRY}"
export IMAGE_TAG="${IMAGE_TAG}"
export HARBOR_USER="${HARBOR_USER:-}"
export HARBOR_PASSWORD="${HARBOR_PASSWORD:-}"
./deploy/scripts/deploy-app.sh "${ENV_NAME}"
if [[ "\${RUN_VPS_SMOKE}" == "1" ]]; then
  ./deploy/scripts/smoke.sh "${ENV_NAME}"
fi
EOF

echo "Remote deploy finished."
