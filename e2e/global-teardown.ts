import { execSync } from 'node:child_process';
import path from 'node:path';

const repoRoot = path.resolve(process.cwd(), '..');
const defaultDatabaseName = 'finalproject_e2e';

function resolveE2eDatabaseName() {
  const fromConnString = process.env.E2E_DATABASE_URL
    ?.split(';')
    .map((part) => part.trim())
    .find((part) => part.toLowerCase().startsWith('database='))
    ?.split('=')[1];

  return process.env.E2E_DATABASE_NAME ?? fromConnString ?? defaultDatabaseName;
}

const e2eDatabaseName = resolveE2eDatabaseName();
const dropDbAfterRun = process.env.E2E_DROP_DB_AFTER_RUN ?? (process.env.CI ? '1' : '0');

function dropE2eDatabase() {
  try {
    execSync(
      `docker compose -f local/docker-compose.yml exec -T postgres dropdb -U postgres --if-exists --force "${e2eDatabaseName}"`,
      { cwd: repoRoot, stdio: 'pipe' },
    );
    console.log(`[e2e] Dropped database "${e2eDatabaseName}" after run.`);
  } catch {
    console.warn(
      `[e2e] Could not drop DB "${e2eDatabaseName}" automatically. Drop manually if needed:\n` +
        `  docker compose -f local/docker-compose.yml exec -T postgres dropdb -U postgres --if-exists --force ${e2eDatabaseName}`,
    );
  }
}

export default async function globalTeardown() {
  if (dropDbAfterRun !== '1') return;
  if (process.env.E2E_SKIP_WEBSERVER === '1') return;

  dropE2eDatabase();
}
