#!/usr/bin/env bash
# Public HTTPS smoke checks from Jenkins (same intent as deploy/scripts/smoke.sh).
# Usage: smoke-http.sh <app_host> <admin_host>
set -euo pipefail

APP_HOST="${1:?APP_HOST required}"
ADMIN_HOST="${2:?ADMIN_HOST required}"

http_status() {
  curl -sS -o /dev/null -w "%{http_code}" "$@"
}

assert_http_status() {
  local label="$1"
  local expected="$2"
  shift 2
  local actual
  actual="$(http_status "$@")"
  local code allowed
  IFS=',' read -r -a allowed <<<"$expected"
  for code in "${allowed[@]}"; do
    if [[ "$actual" == "$code" ]]; then
      echo "  OK ${label} (HTTP ${actual})"
      return 0
    fi
  done
  echo "FAIL ${label}: expected HTTP ${expected}, got ${actual}" >&2
  exit 1
}

echo "Smoke test: app=${APP_HOST} admin=${ADMIN_HOST}"

assert_http_status "frontend ${APP_HOST}" "200" "https://${APP_HOST}/"
assert_http_status "admin ${ADMIN_HOST}" "200" "https://${ADMIN_HOST}/"

assert_http_status "api via app host" "400" \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{}' \
  "https://${APP_HOST}/api/v1/auth/login"

assert_http_status "api via admin host" "400" \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{}' \
  "https://${ADMIN_HOST}/api/v1/auth/login"

echo "Smoke checks passed."
