import { execSync } from 'node:child_process';
import path from 'node:path';
import { dropDatabaseIfExists, resolveE2eDatabaseName, useDirectPostgresCli } from './postgres-cli';

const repoRoot = path.resolve(process.cwd(), '..');
const e2eDatabaseName = resolveE2eDatabaseName('baseproject_e2e');
const dropDbAfterRun = process.env.E2E_DROP_DB_AFTER_RUN ?? (process.env.CI ? '1' : '0');

function dropE2eDatabaseDocker() {
  execSync(
    `docker compose -f local/docker-compose.yml exec -T postgres dropdb -U postgres --if-exists --force "${e2eDatabaseName}"`,
    { cwd: repoRoot, stdio: 'pipe' },
  );
}

function dropE2eDatabase() {
  try {
    if (useDirectPostgresCli()) {
      dropDatabaseIfExists(e2eDatabaseName);
    } else {
      dropE2eDatabaseDocker();
    }
    console.log(`[e2e] Dropped database "${e2eDatabaseName}" after run.`);
  } catch {
    console.warn(`[e2e] Could not drop DB "${e2eDatabaseName}" automatically.`);
  }
}

export default async function globalTeardown() {
  if (dropDbAfterRun !== '1') return;
  if (process.env.E2E_SKIP_WEBSERVER === '1') return;

  dropE2eDatabase();
}
