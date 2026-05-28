#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEPLOY="$ROOT/deploy"
COMPOSE_STACK="$DEPLOY/compose/stack.yml"
COMPOSE_TRAEFIK="$DEPLOY/compose/traefik.yml"
COMPOSE_INFRA="$DEPLOY/compose/infra.yml"

deploy_env_files() {
  local env_name="${1:-staging}"
  local common="$DEPLOY/env/shared/app.common.env"
  local app="$DEPLOY/env/${env_name}/app.env"
  if [[ ! -f "$common" ]]; then
    echo "Missing $common — copy from app.common.env.example" >&2
    exit 1
  fi
  if [[ ! -f "$app" ]]; then
    echo "Missing $app — copy from app.env.example" >&2
    exit 1
  fi
  echo "--env-file" "$common" "--env-file" "$app"
}

compose_stack() {
  # shellcheck disable=SC2046
  docker compose $(deploy_env_files "$1") -f "$COMPOSE_STACK" "${@:2}"
}

infra_env_files() {
  local env_name="${1:-staging}"
  local common="$DEPLOY/env/shared/app.common.env"
  local infra="$DEPLOY/env/${env_name}/infra.env"
  if [[ ! -f "$common" ]]; then
    echo "Missing $common — copy from app.common.env.example" >&2
    exit 1
  fi
  if [[ ! -f "$infra" ]]; then
    echo "Missing $infra — copy from infra.env.example" >&2
    exit 1
  fi
  echo "--env-file" "$common" "--env-file" "$infra"
}

compose_infra() {
  # shellcheck disable=SC2046
  docker compose $(infra_env_files "$1") -f "$COMPOSE_INFRA" "${@:2}"
}

release_logs_dir() {
  local env_name="${1:-staging}"
  local dir="$DEPLOY/releases/$env_name"
  mkdir -p "$dir"
  echo "$dir"
}
