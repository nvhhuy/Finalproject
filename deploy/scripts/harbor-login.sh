#!/usr/bin/env bash
# Log in to Harbor (or any registry) before pull/up on VPS or CI agent.
# Requires: HARBOR_USER, HARBOR_PASSWORD, and IMAGE_REGISTRY (or HARBOR_HOST).
set -euo pipefail

: "${HARBOR_USER:?HARBOR_USER is required}"
: "${HARBOR_PASSWORD:?HARBOR_PASSWORD is required}"

if [[ -z "${IMAGE_REGISTRY:-}" && -z "${HARBOR_HOST:-}" ]]; then
  echo "Set IMAGE_REGISTRY or HARBOR_HOST for docker login." >&2
  exit 1
fi

HARBOR_HOST="${HARBOR_HOST:-${IMAGE_REGISTRY%%/*}}"

echo "Logging in to registry (${HARBOR_HOST})..."
echo "${HARBOR_PASSWORD}" | docker login "${HARBOR_HOST}" -u "${HARBOR_USER}" --password-stdin
