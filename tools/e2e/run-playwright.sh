#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Prefer Node 20+ (Next.js 15 requirement). Load nvm when available.
if [[ -s "${NVM_DIR:-$HOME/.nvm}/nvm.sh" ]]; then
  # shellcheck source=/dev/null
  source "${NVM_DIR:-$HOME/.nvm}/nvm.sh"
  if [[ -f "$ROOT/.nvmrc" ]]; then
    nvm install >/dev/null 2>&1 || true
    nvm use >/dev/null 2>&1 || true
  else
    nvm use 20 >/dev/null 2>&1 || true
  fi
fi

NODE_MAJOR="$(node -p "process.versions.node.split('.')[0]" 2>/dev/null || echo 0)"
if [[ "$NODE_MAJOR" -lt 20 ]]; then
  echo "Playwright requires Node.js 20+. Current: $(node -v 2>/dev/null || echo 'not found')." >&2
  echo "Run: nvm use   (repo .nvmrc) or install Node 20+." >&2
  exit 1
fi

cd "$ROOT/e2e"
npm install
npm run install:browsers

# Keep UI e2e isolated from developer data by default.
export E2E_DATABASE_URL="${E2E_DATABASE_URL:-Host=localhost;Port=5432;Database=baseproject_e2e;Username=postgres;Password=postgres}"
export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-baseproject_e2e}"
if [[ -n "${CI:-}" ]]; then
  export E2E_DROP_DB_AFTER_RUN="${E2E_DROP_DB_AFTER_RUN:-1}"
  export E2E_POSTGRES_CLI="${E2E_POSTGRES_CLI:-1}"
else
  export E2E_DROP_DB_AFTER_RUN="${E2E_DROP_DB_AFTER_RUN:-0}"
fi

npm test "$@"
