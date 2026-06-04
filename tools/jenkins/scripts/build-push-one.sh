#!/usr/bin/env bash
# Build and push a single service image. Usage: build-push-one.sh <api|worker|migrate|frontend|admin>
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"

SERVICE="${1:?service name required (api|worker|migrate|frontend|admin)}"

: "${IMAGE_REGISTRY:?IMAGE_REGISTRY is required}"
: "${IMAGE_TAG:?IMAGE_TAG is required}"

image="${IMAGE_REGISTRY}/${SERVICE}:${IMAGE_TAG}"

case "${SERVICE}" in
  api)
    docker build -f deploy/docker/api/Dockerfile -t "${image}" .
    ;;
  worker)
    docker build -f deploy/docker/worker/Dockerfile -t "${image}" .
    ;;
  migrate)
    docker build -f deploy/docker/migrate/Dockerfile -t "${image}" .
    ;;
  frontend)
    docker build -f deploy/docker/frontend/Dockerfile \
      --build-arg "API_PROXY_TARGET=${API_PROXY_TARGET:-http://api:8080}" \
      -t "${image}" .
    ;;
  admin)
    docker build -f deploy/docker/admin/Dockerfile -t "${image}" .
    ;;
  *)
    echo "Unknown service: ${SERVICE}" >&2
    exit 1
    ;;
esac

echo "Pushing ${image}..."
docker push "${image}"
echo "Done: ${image}"
