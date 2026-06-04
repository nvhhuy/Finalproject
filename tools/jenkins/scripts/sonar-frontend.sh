#!/usr/bin/env bash
# SonarQube analysis — Frontend (Next.js / TypeScript).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

: "${SONAR_HOST_URL:?SONAR_HOST_URL is required}"
: "${SONAR_TOKEN:?SONAR_TOKEN is required}"

SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY_FRONTEND:-baseproject-frontend}"

echo "Sonar frontend (${SONAR_PROJECT_KEY})..."

docker run --rm \
  -e SONAR_HOST_URL="${SONAR_HOST_URL}" \
  -e SONAR_TOKEN="${SONAR_TOKEN}" \
  -v "${ROOT}:/usr/src" \
  sonarsource/sonar-scanner-cli:latest \
  -Dsonar.projectKey="${SONAR_PROJECT_KEY}" \
  -Dsonar.projectName="BaseProject Frontend" \
  -Dsonar.projectBaseDir=/usr/src/src/Frontend \
  -Dsonar.sources=app,components,lib \
  -Dsonar.exclusions=**/node_modules/**,**/.next/**,**/public/** \
  -Dsonar.sourceEncoding=UTF-8 \
  -Dsonar.host.url="${SONAR_HOST_URL}" \
  -Dsonar.token="${SONAR_TOKEN}"

echo "Sonar frontend submitted: ${SONAR_PROJECT_KEY}"
