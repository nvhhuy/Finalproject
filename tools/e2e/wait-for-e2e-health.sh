#!/usr/bin/env bash
set -euo pipefail

API_URL="${E2E_API_URL:-http://localhost:5076}"
FE_URL="${E2E_FRONTEND_URL:-http://localhost:3000}"
ADMIN_URL="${E2E_ADMIN_URL:-http://localhost:5174}"
TIMEOUT_SEC="${E2E_HEALTH_TIMEOUT_SEC:-180}"

wait_url() {
  local url="$1"
  local deadline=$((SECONDS + TIMEOUT_SEC))
  while (( deadline > SECONDS )); do
    local code
    code="$(curl -sS -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || echo '000')"
    if [[ "$code" =~ ^[23][0-9][0-9]$ ]]; then
      echo "[e2e] Healthy ($code): $url"
      return 0
    fi
    sleep 2
  done
  echo "[e2e] ERROR: Timed out waiting for $url" >&2
  return 1
}

wait_url "${API_URL%/}/health/live"
wait_url "$FE_URL/"
wait_url "$ADMIN_URL/"
