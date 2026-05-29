#!/usr/bin/env bash
# Ensures Playwright webServer ports are free before starting tests.
set -euo pipefail

API_PORT="${E2E_API_PORT:-5076}"
FE_PORT="${E2E_FRONTEND_PORT:-3000}"
ADMIN_PORT="${E2E_ADMIN_PORT:-5174}"
WAIT_TIMEOUT_SEC="${E2E_PORT_WAIT_TIMEOUT_SEC:-120}"
CLEAN_PORTS="${E2E_CLEAN_PORTS:-0}"

port_in_use() {
  local port="$1"
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1
    return $?
  fi
  if command -v ss >/dev/null 2>&1; then
    ss -ltnH "sport = :$port" 2>/dev/null | grep -q .
    return $?
  fi
  return 1
}

describe_port_owner() {
  local port="$1"
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$port" -sTCP:LISTEN 2>/dev/null | awk 'NR>1 {printf "  - PID %s %s\n", $2, $1}'
    return
  fi
  echo "  (install lsof to inspect port $port)"
}

wait_for_port() {
  local port="$1"
  local deadline=$((SECONDS + WAIT_TIMEOUT_SEC))
  while (( deadline > SECONDS )); do
    if ! port_in_use "$port"; then
      echo "[e2e] Port $port is free."
      return 0
    fi
    echo "[e2e] Waiting for port $port to become free ($((deadline - SECONDS))s left)..."
    sleep 2
  done
  echo "[e2e] ERROR: Port $port still in use after ${WAIT_TIMEOUT_SEC}s." >&2
  describe_port_owner "$port"
  return 1
}

clean_port() {
  local port="$1"
  if ! port_in_use "$port"; then
    return 0
  fi
  if [[ "$CLEAN_PORTS" != "1" ]]; then
    echo "[e2e] Port $port is in use. Set E2E_CLEAN_PORTS=1 to stop listeners on this port only." >&2
    describe_port_owner "$port"
    return 1
  fi
  if command -v lsof >/dev/null 2>&1; then
    local pids
    pids="$(lsof -ti tcp:"$port" 2>/dev/null || true)"
    if [[ -n "$pids" ]]; then
      echo "[e2e] Stopping processes on port $port: $pids"
      # shellcheck disable=SC2086
      kill $pids 2>/dev/null || true
      sleep 2
    fi
  fi
  wait_for_port "$port"
}

for port in "$API_PORT" "$FE_PORT" "$ADMIN_PORT"; do
  if port_in_use "$port"; then
    if [[ "$CLEAN_PORTS" == "1" ]]; then
      clean_port "$port"
    else
      wait_for_port "$port"
    fi
  else
    echo "[e2e] Port $port is already free."
  fi
done

echo "[e2e] Port preparation complete."
