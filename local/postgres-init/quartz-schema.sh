#!/bin/sh
# Idempotent Quartz schema bootstrap for local docker compose (dev only).
set -eu

PGHOST="${PGHOST:-postgres}"
PGPORT="${PGPORT:-5432}"
PGDATABASE="${PGDATABASE:-baseproject}"
PGUSER="${PGUSER:-postgres}"
export PGPASSWORD="${PGPASSWORD:-postgres}"

until pg_isready -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" >/dev/null 2>&1; do
  echo "Waiting for Postgres at ${PGHOST}:${PGPORT}..."
  sleep 2
done

if psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -tAc \
  "SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'qrtz_job_details'" \
  | grep -q 1; then
  echo "Quartz tables already exist, skipping."
  exit 0
fi

echo "Applying Quartz schema to ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE}..."
psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -v ON_ERROR_STOP=1 -f /schema/tables_postgres.sql
echo "Quartz schema applied."
