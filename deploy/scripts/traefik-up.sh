#!/usr/bin/env bash
# Start Traefik edge proxy. Usage: ./deploy/scripts/traefik-up.sh
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DEPLOY="$ROOT/deploy"
TRAEFIK_ENV="$DEPLOY/env/shared/traefik.env"

if [[ ! -f "$TRAEFIK_ENV" ]]; then
  echo "Missing $TRAEFIK_ENV — copy from traefik.env.example" >&2
  exit 1
fi

docker network create proxy 2>/dev/null || true
docker compose --env-file "$TRAEFIK_ENV" -f "$DEPLOY/compose/traefik.yml" up -d
