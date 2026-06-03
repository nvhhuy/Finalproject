# Shared helpers for tools/ci/run-ci-local.sh
# shellcheck shell=bash

if [[ -n "${CI_COMMON_LOADED:-}" ]]; then
  return 0
fi
CI_COMMON_LOADED=1

CI_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CI_ROOT="$(cd "$CI_LIB_DIR/../../.." && pwd)"
CI_COMPOSE_FILE="$CI_ROOT/local/docker-compose.yml"

export FORCE_JAVASCRIPT_ACTIONS_TO_NODE24="${FORCE_JAVASCRIPT_ACTIONS_TO_NODE24:-true}"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-postgres}"
PGPASSWORD="${PGPASSWORD:-postgres}"
export PGPASSWORD PGHOST PGPORT PGUSER

log_step() { printf '\n\033[1;36m==>\033[0m %s\n' "$*"; }
log_ok()   { printf '\033[1;32m✓\033[0m %s\n' "$*"; }
log_warn() { printf '\033[1;33m!\033[0m %s\n' "$*" >&2; }
log_err()  { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; }

require_cmd() {
  local missing=()
  for c in "$@"; do
    command -v "$c" >/dev/null 2>&1 || missing+=("$c")
  done
  if ((${#missing[@]} > 0)); then
    log_err "Missing required commands: ${missing[*]}"
    exit 1
  fi
}

use_nvm_if_available() {
  if [[ -s "${NVM_DIR:-$HOME/.nvm}/nvm.sh" ]]; then
    # shellcheck source=/dev/null
    source "${NVM_DIR:-$HOME/.nvm}/nvm.sh"
    if [[ -f "$CI_ROOT/.nvmrc" ]]; then
      nvm install >/dev/null 2>&1 || true
      nvm use >/dev/null 2>&1 || true
    fi
  fi
}

require_node_min() {
  local min_major="${1:-20}"
  use_nvm_if_available
  local major
  major="$(node -p "process.versions.node.split('.')[0]" 2>/dev/null || echo 0)"
  if [[ "$major" -lt "$min_major" ]]; then
    log_err "Node.js ${min_major}+ required (CI uses 24). Current: $(node -v 2>/dev/null || echo missing)."
    log_err "Run: nvm use   (repo .nvmrc) or install Node ${min_major}+."
    exit 1
  fi
}

compose() {
  docker compose -f "$CI_COMPOSE_FILE" "$@"
}

compose_services_running() {
  local svc
  for svc in "$@"; do
    local cid
    cid="$(compose ps -q "$svc" 2>/dev/null || true)"
    if [[ -z "$cid" ]]; then
      return 1
    fi
    local status
    status="$(docker inspect -f '{{.State.Status}}' "$cid" 2>/dev/null || echo missing)"
    [[ "$status" == "running" ]] || return 1
  done
  return 0
}

ensure_compose_services() {
  if [[ "${CI_SKIP_DOCKER:-}" == "1" ]]; then
    log_warn "CI_SKIP_DOCKER=1 — assuming Postgres/Redis/MinIO already reachable on localhost."
    return 0
  fi

  require_cmd docker
  if ! docker info >/dev/null 2>&1; then
    log_err "Docker is not running. Start Docker Desktop (or the daemon), then retry."
    exit 1
  fi

  if compose_services_running "$@"; then
    log_ok "Docker services already up: $*"
    return 0
  fi

  log_step "Starting Docker services: $*"
  compose up -d "$@"
}

wait_for_postgres() {
  log_step "Waiting for Postgres at ${PGHOST}:${PGPORT}"
  local i
  for i in $(seq 1 60); do
    if command -v pg_isready >/dev/null 2>&1 && pg_isready -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" >/dev/null 2>&1; then
      log_ok "Postgres is ready (host pg_isready)"
      return 0
    fi
    if compose_services_running postgres; then
      if compose exec -T postgres pg_isready -U "$PGUSER" >/dev/null 2>&1; then
        log_ok "Postgres is ready (docker compose)"
        return 0
      fi
    fi
    sleep 1
  done
  log_err "Postgres did not become ready in time."
  exit 1
}

wait_for_minio() {
  log_step "Waiting for MinIO at http://localhost:9000"
  local i
  for i in $(seq 1 60); do
    if curl -fsS "http://localhost:9000/minio/health/live" >/dev/null 2>&1; then
      log_ok "MinIO is ready"
      return 0
    fi
    sleep 2
  done
  log_err "MinIO did not become ready. Run: docker compose -f local/docker-compose.yml up -d minio minio-init"
  exit 1
}

ensure_minio_bucket() {
  local bucket="${1:-media}"
  if compose_services_running minio; then
    compose run --rm --no-deps minio-init >/dev/null 2>&1 || true
    return 0
  fi
  docker run --rm --network host minio/mc:latest /bin/sh -c "
    mc alias set local http://localhost:9000 minio minio123 2>/dev/null || true
    mc mb --ignore-existing local/${bucket}
  " >/dev/null 2>&1 || true
}

create_database() {
  local db="$1"
  log_step "Ensuring database exists: ${db}"

  if command -v createdb >/dev/null 2>&1; then
    PGPASSWORD="$PGPASSWORD" createdb -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" "$db" 2>/dev/null || true
    log_ok "Database ${db} (host createdb)"
    return 0
  fi

  if compose_services_running postgres; then
    compose exec -T postgres psql -U "$PGUSER" -tc \
      "SELECT 1 FROM pg_database WHERE datname = '${db}'" | grep -q 1 \
      || compose exec -T postgres createdb -U "$PGUSER" "$db" 2>/dev/null || true
    log_ok "Database ${db} (docker exec)"
    return 0
  fi

  log_err "Cannot create database ${db}: install postgresql client (createdb) or start local/docker-compose postgres."
  exit 1
}

run_dotnet_test() {
  local label="$1"
  shift
  log_step "$label"
  (cd "$CI_ROOT" && dotnet test "$@")
  log_ok "$label"
}

run_npm_ci() {
  local dir="$1"
  local label="$2"
  log_step "$label"
  (cd "$CI_ROOT/$dir" && npm ci)
  log_ok "$label"
}
