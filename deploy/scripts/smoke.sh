#!/usr/bin/env bash
# Smoke checks after deploy. Usage:
#   ./deploy/scripts/smoke.sh [staging|production]
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib.sh
source "$SCRIPT_DIR/lib.sh"

ENV_NAME="${1:-staging}"
COMMON="$DEPLOY/env/shared/app.common.env"
APP="$DEPLOY/env/${ENV_NAME}/app.env"

if [[ ! -f "$COMMON" || ! -f "$APP" ]]; then
  echo "Missing env files for $ENV_NAME (app.common.env + app.env)." >&2
  exit 1
fi

APP_HOST="$(awk -F= '$1=="APP_HOST"{print $2}' "$COMMON" | tr -d '\r')"
ADMIN_HOST="$(awk -F= '$1=="ADMIN_HOST"{print $2}' "$COMMON" | tr -d '\r')"
if [[ -z "$APP_HOST" || -z "$ADMIN_HOST" ]]; then
  echo "APP_HOST / ADMIN_HOST must be set in $COMMON" >&2
  exit 1
fi

# Returns HTTP status code (curl must not use -f so 4xx are visible).
http_status() {
  curl -sS -o /dev/null -w "%{http_code}" "$@"
}

# Fails unless status is one of the allowed codes (comma-separated).
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
      echo "  OK $label (HTTP $actual)"
      return 0
    fi
  done
  echo "FAIL $label: expected HTTP ${expected}, got ${actual}" >&2
  exit 1
}

echo "Checking internal API readiness..."
compose_stack "$ENV_NAME" exec -T api curl -fsS http://127.0.0.1:8080/health/ready >/dev/null
echo "  OK internal /health/ready"

echo "Checking public routes via Traefik..."
assert_http_status "frontend ${APP_HOST}" "200" "https://${APP_HOST}/"
assert_http_status "admin ${ADMIN_HOST}" "200" "https://${ADMIN_HOST}/"

# Anonymous POST with empty body should reach API and return validation error (400), not 5xx.
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

echo "Smoke checks passed for ${ENV_NAME}."
"$SCRIPT_DIR/manifest.sh" "$ENV_NAME" "smoke_passed"
