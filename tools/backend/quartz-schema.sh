#!/usr/bin/env bash
# Apply Quartz.NET PostgreSQL job-store tables. Run from repo root: ./tools/backend/quartz-schema.sh
# Defaults match Worker appsettings.Development.json; override via PGHOST, PGPORT, PGDATABASE, PGUSER, PGPASSWORD.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SQL="$ROOT/deploy/sql/quartz/tables_postgres.sql"

PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-baseproject}"
PGUSER="${PGUSER:-postgres}"
export PGPASSWORD="${PGPASSWORD:-postgres}"

if ! command -v psql >/dev/null 2>&1; then
  echo "psql not found. Install PostgreSQL client tools or run deploy/sql/quartz/tables_postgres.sql manually." >&2
  exit 1
fi

echo "Applying Quartz schema to ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE} (drops existing qrtz_* tables)."
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 -f "$SQL"
echo "Quartz schema applied."
