#!/usr/bin/env bash
# Apply EF Core migrations (write database). Run from repo root: ./tools/backend/migrate.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BACKEND="$ROOT/src/Backend"
cd "$BACKEND"
dotnet ef database update --project Infrastructure --startup-project Api --context ApplicationDbContext
echo "Database updated (ApplicationDbContext)."
