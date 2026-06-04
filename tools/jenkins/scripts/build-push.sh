#!/usr/bin/env bash
# Build and push all images in parallel (local/CLI fallback).
# Jenkins pipeline uses build-push-one.sh in parallel stages instead.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

: "${IMAGE_REGISTRY:?IMAGE_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"
: "${HARBOR_USER:?HARBOR_USER is required}"
: "${HARBOR_PASSWORD:?HARBOR_PASSWORD is required}"

"${SCRIPT_DIR}/harbor-login.sh"

SERVICES=(api worker migrate frontend admin)
pids=()
failed=0

for svc in "${SERVICES[@]}"; do
  echo "Starting parallel build: ${svc}"
  "${SCRIPT_DIR}/build-push-one.sh" "${svc}" &
  pids+=($!)
done

for pid in "${pids[@]}"; do
  if ! wait "${pid}"; then
    failed=1
  fi
done

if [[ "${failed}" -ne 0 ]]; then
  echo "One or more image builds failed." >&2
  exit 1
fi

echo "All images pushed with tag: ${IMAGE_TAG}"
