#!/usr/bin/env bash
# Run all Sonar module scans in parallel (local/CLI fallback).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

: "${SONAR_HOST_URL:?SONAR_HOST_URL is required}"
: "${SONAR_TOKEN:?SONAR_TOKEN is required}"

pids=()
failed=0

for script in sonar-backend.sh sonar-frontend.sh sonar-admin.sh; do
  echo "Starting parallel scan: ${script}"
  "${SCRIPT_DIR}/${script}" &
  pids+=($!)
done

for pid in "${pids[@]}"; do
  if ! wait "${pid}"; then
    failed=1
  fi
done

if [[ "${failed}" -ne 0 ]]; then
  echo "One or more Sonar scans failed." >&2
  exit 1
fi

"${SCRIPT_DIR}/sonar-quality-gate.sh"
