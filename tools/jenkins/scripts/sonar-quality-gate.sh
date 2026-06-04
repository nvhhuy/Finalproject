#!/usr/bin/env bash
# Wait for SonarQube quality gates on all module projects (after parallel scans).
set -euo pipefail

: "${SONAR_HOST_URL:?SONAR_HOST_URL is required}"
: "${SONAR_TOKEN:?SONAR_TOKEN is required}"

PROJECTS=(
  "${SONAR_PROJECT_KEY_BACKEND:-baseproject-backend}"
  "${SONAR_PROJECT_KEY_FRONTEND:-baseproject-frontend}"
  "${SONAR_PROJECT_KEY_ADMIN:-baseproject-admin}"
)

MAX_WAIT_SEC="${SONAR_GATE_MAX_WAIT_SEC:-600}"
POLL_SEC="${SONAR_GATE_POLL_SEC:-15}"

gate_status() {
  local project_key="$1"
  curl -sf -u "${SONAR_TOKEN}:" \
    "${SONAR_HOST_URL}/api/qualitygates/project_status?projectKey=${project_key}" \
    | python3 -c "import json,sys; print(json.load(sys.stdin)['projectStatus']['status'])"
}

wait_for_project() {
  local project_key="$1"
  local elapsed=0
  local status="NONE"

  echo "Waiting for quality gate: ${project_key} (max ${MAX_WAIT_SEC}s)..."

  while [[ "${elapsed}" -lt "${MAX_WAIT_SEC}" ]]; do
    status="$(gate_status "${project_key}" 2>/dev/null || echo "PENDING")"
    case "${status}" in
      OK)
        echo "  OK ${project_key}"
        return 0
        ;;
      ERROR)
        echo "  FAIL ${project_key}: quality gate ERROR" >&2
        return 1
        ;;
      *)
        sleep "${POLL_SEC}"
        elapsed=$((elapsed + POLL_SEC))
        ;;
    esac
  done

  echo "  TIMEOUT ${project_key} (last status: ${status})" >&2
  return 1
}

failed=0
for project in "${PROJECTS[@]}"; do
  wait_for_project "${project}" || failed=1
done

if [[ "${failed}" -ne 0 ]]; then
  echo "One or more SonarQube quality gates failed." >&2
  exit 1
fi

echo "All SonarQube quality gates passed."
