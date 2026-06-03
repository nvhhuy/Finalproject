#!/usr/bin/env bash
# Run the same checks as .github/workflows/ci.yml on your machine (no GitHub Actions minutes).
#
# Usage:
#   ./tools/ci/run-ci-local.sh              # all jobs
#   ./tools/ci/run-ci-local.sh backend      # one job
#   ./tools/ci/run-ci-local.sh --list
#   CI_SKIP_DOCKER=1 ./tools/ci/run-ci-local.sh media   # services already up
#
# Prerequisites: Docker (for integration jobs), .NET 8 SDK, Node 24 (nvm use).
# Docs: tools/ci/README.md

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

ALL_JOBS=(admin frontend backend media playwright)

usage() {
  cat <<EOF
Run GitHub CI workflows locally (mirrors .github/workflows/ci.yml).

Usage:
  $(basename "$0") [job...] [options]

Jobs (default: all):
  admin       Admin lint + build + audit
  frontend    Frontend lint + typecheck + build + audit
  backend     Backend unit tests + API integration (Postgres)
  media       Media integration tests (Postgres + MinIO)
  playwright  Playwright E2E (Postgres + Redis)

Options:
  --list          List job names and exit
  --skip-docker   Alias for CI_SKIP_DOCKER=1 (services already running)
  -h, --help      Show this help

Environment:
  CI_SKIP_DOCKER=1     Do not start docker compose; use localhost services
  PGHOST PGPORT PGUSER PGPASSWORD   Postgres connection (defaults match CI/local)

Examples:
  $(basename "$0")
  $(basename "$0") backend media
  CI_SKIP_DOCKER=1 $(basename "$0") playwright

See tools/ci/README.md for setup and troubleshooting.
EOF
}

list_jobs() {
  printf 'CI jobs (same names as GitHub workflow):\n'
  for j in "${ALL_JOBS[@]}"; do
    printf '  - %s\n' "$j"
  done
}

run_admin_quality() {
  require_cmd npm
  require_node_min 20
  run_npm_ci src/Admin "Admin: npm ci"
  (cd "$CI_ROOT/src/Admin" && npm run lint)
  (cd "$CI_ROOT/src/Admin" && npm run build)
  (cd "$CI_ROOT/src/Admin" && npm audit --audit-level=high) || log_warn "Admin npm audit reported issues (CI continues-on-error)"
  log_ok "Admin quality checks"
}

run_frontend_quality() {
  require_cmd npm
  require_node_min 20
  run_npm_ci src/Frontend "Frontend: npm ci"
  (cd "$CI_ROOT/src/Frontend" && npm run lint)
  (cd "$CI_ROOT/src/Frontend" && npm run typecheck)
  (cd "$CI_ROOT/src/Frontend" && npm run build)
  (cd "$CI_ROOT/src/Frontend" && npm audit --audit-level=high) || log_warn "Frontend npm audit reported issues (CI continues-on-error)"
  log_ok "Frontend quality checks"
}

run_backend() {
  require_cmd dotnet
  run_dotnet_test "Backend: unit tests" src/Backend \
    --filter "FullyQualifiedName!~Api.IntegrationTests" \
    --configuration Release \
    --verbosity minimal

  ensure_compose_services postgres
  wait_for_postgres
  create_database baseproject_integration

  export IntegrationTesting__DisableRateLimiting=true
  export TEST_DATABASE_CONNECTION_STRING="Host=${PGHOST};Port=${PGPORT};Database=baseproject_integration;Username=${PGUSER};Password=${PGPASSWORD}"

  run_dotnet_test "Backend: API integration tests" \
    src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj \
    --configuration Release \
    --verbosity normal \
    --blame-hang-timeout 3m
}

run_backend_media() {
  require_cmd dotnet curl
  ensure_compose_services postgres minio
  wait_for_postgres
  wait_for_minio
  ensure_minio_bucket media
  create_database baseproject_media_integration

  export IntegrationTesting__DisableRateLimiting=true
  export INTEGRATION_MEDIA_ENABLED=true
  export TEST_DATABASE_CONNECTION_STRING="Host=${PGHOST};Port=${PGPORT};Database=baseproject_media_integration;Username=${PGUSER};Password=${PGPASSWORD}"
  export ObjectStorage__ServiceUrl=http://localhost:9000
  export ObjectStorage__AccessKey=minio
  export ObjectStorage__SecretKey=minio123
  export ObjectStorage__Bucket=media
  export ObjectStorage__ForcePathStyle=true
  export ObjectStorage__Region=us-east-1

  run_dotnet_test "Backend: media integration tests" \
    src/Backend/Tests/Api.IntegrationTests/Api.IntegrationTests.csproj \
    --filter "FullyQualifiedName~MediaIntegrationTests" \
    --configuration Release \
    --verbosity normal \
    --blame-hang-timeout 3m
}

run_playwright() {
  require_cmd dotnet npm bash
  require_node_min 20

  ensure_compose_services postgres redis
  wait_for_postgres

  run_npm_ci src/Frontend "Playwright: Frontend npm ci"
  run_npm_ci src/Admin "Playwright: Admin npm ci"

  export CI=true
  export E2E_POSTGRES_CLI=1
  export E2E_POSTGRES_HOST="${PGHOST}"
  export E2E_POSTGRES_USER="${PGUSER}"
  export E2E_POSTGRES_PASSWORD="${PGPASSWORD}"
  export E2E_PREPARE_PORTS=1
  export E2E_STABLE_PARALLEL=1
  export E2E_WORKERS="${E2E_WORKERS:-2}"

  log_step "Playwright E2E"
  bash "$CI_ROOT/tools/e2e/run-playwright.sh"
  log_ok "Playwright E2E"
}

run_job() {
  case "$1" in
    admin) run_admin_quality ;;
    frontend) run_frontend_quality ;;
    backend) run_backend ;;
    media) run_backend_media ;;
    playwright) run_playwright ;;
    *)
      log_err "Unknown job: $1"
      exit 1
      ;;
  esac
}

main() {
  local jobs=()
  for arg in "$@"; do
    case "$arg" in
      -h|--help) usage; exit 0 ;;
      --list) list_jobs; exit 0 ;;
      --skip-docker) export CI_SKIP_DOCKER=1 ;;
      admin|frontend|backend|media|playwright) jobs+=("$arg") ;;
      *)
        log_err "Unknown argument: $arg"
        usage >&2
        exit 1
        ;;
    esac
  done

  if ((${#jobs[@]} == 0)); then
    jobs=("${ALL_JOBS[@]}")
  fi

  log_step "Local CI — jobs: ${jobs[*]}"
  log_step "Repo: $CI_ROOT"

  local failed=()
  local start total
  start=$(date +%s)
  for job in "${jobs[@]}"; do
    log_step "━━━ Job: $job ━━━"
    if ( run_job "$job" ); then
      log_ok "Job passed: $job"
    else
      log_err "Job failed: $job"
      failed+=("$job")
    fi
  done
  total=$(($(date +%s) - start))

  if ((${#failed[@]} > 0)); then
    log_err "Local CI failed (${total}s): ${failed[*]}"
    exit 1
  fi

  log_ok "Local CI passed all jobs (${total}s): ${jobs[*]}"
}

main "$@"
