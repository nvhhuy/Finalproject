#!/usr/bin/env bash
# SonarQube analysis — Backend (.NET Api + Worker via Backend.sln).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

: "${SONAR_HOST_URL:?SONAR_HOST_URL is required}"
: "${SONAR_TOKEN:?SONAR_TOKEN is required}"

SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY_BACKEND:-baseproject-backend}"
SONAR_PROPS="$ROOT/tools/jenkins/sonar/backend.properties"

if ! dotnet tool list -g | grep -q dotnet-sonarscanner; then
  dotnet tool install --global dotnet-sonarscanner
fi

export PATH="${HOME}/.dotnet/tools:${PATH}"

echo "Sonar backend (${SONAR_PROJECT_KEY})..."

dotnet sonarscanner begin \
  /k:"${SONAR_PROJECT_KEY}" \
  /n:"BaseProject Backend" \
  /d:sonar.host.url="${SONAR_HOST_URL}" \
  /d:sonar.token="${SONAR_TOKEN}" \
  /d:sonar.projectBaseDir="${ROOT}" \
  /d:project.settings="${SONAR_PROPS}"

dotnet build src/Backend/Backend.sln -c Release --no-restore 2>/dev/null || \
  dotnet build src/Backend/Backend.sln -c Release

dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"

echo "Sonar backend submitted: ${SONAR_PROJECT_KEY}"
