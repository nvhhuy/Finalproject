#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT/e2e"
npm install
npm run install:browsers

# Keep UI e2e isolated from developer data by default.
export E2E_DATABASE_URL="${E2E_DATABASE_URL:-Host=localhost;Port=5432;Database=finalproject_e2e;Username=postgres;Password=postgres}"
export E2E_DATABASE_NAME="${E2E_DATABASE_NAME:-finalproject_e2e}"
if [[ -n "${CI:-}" ]]; then
  export E2E_DROP_DB_AFTER_RUN="${E2E_DROP_DB_AFTER_RUN:-1}"
else
  export E2E_DROP_DB_AFTER_RUN="${E2E_DROP_DB_AFTER_RUN:-0}"
fi

npm test "$@"
