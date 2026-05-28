import { execSync } from 'node:child_process';
import net from 'node:net';
import path from 'node:path';
import {
  ensureDatabaseExists,
  resetE2eAuthData,
  resolveE2eDatabaseName,
  useDirectPostgresCli,
} from './postgres-cli';

const repoRoot = path.resolve(process.cwd(), '..');

const postgresHost = process.env.E2E_POSTGRES_HOST ?? 'localhost';
const postgresPort = Number(process.env.E2E_POSTGRES_PORT ?? '5432');
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@localhost.com';
const e2eDatabaseName = resolveE2eDatabaseName('baseproject_e2e');

function canConnect(host: string, port: number, timeoutMs = 3_000): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = net.connect({ host, port });
    const done = (ok: boolean) => {
      socket.destroy();
      resolve(ok);
    };
    socket.setTimeout(timeoutMs);
    socket.once('connect', () => done(true));
    socket.once('timeout', () => done(false));
    socket.once('error', () => done(false));
  });
}

function ensureE2eDatabaseExistsDocker() {
  const databaseName = e2eDatabaseName.replace(/'/g, "''");
  execSync(
    `docker compose -f local/docker-compose.yml exec -T postgres sh -lc "createdb -U postgres '${databaseName}' >/dev/null 2>&1 || true"`,
    { cwd: repoRoot, stdio: 'pipe' },
  );
}

function resetAuthDataForE2eDocker() {
  const email = adminEmail.replace(/'/g, "''");
  const databaseName = e2eDatabaseName.replace(/'/g, "''");
  const sql =
    `UPDATE "Users" SET "TwoFactorSecret" = NULL; DELETE FROM "Users" WHERE "Email" = '${email}';`;
  execSync(
    `docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d "${databaseName}" -v ON_ERROR_STOP=1 -c "${sql}"`,
    { cwd: repoRoot, stdio: 'pipe' },
  );
}

/** Ensures Playwright uses a dedicated DB to avoid polluting local dev data. */
function ensureE2eDatabaseExists() {
  try {
    if (useDirectPostgresCli()) {
      ensureDatabaseExists(e2eDatabaseName);
    } else {
      ensureE2eDatabaseExistsDocker();
    }
    console.log(`[e2e] Ensured dedicated database "${e2eDatabaseName}" exists.`);
  } catch {
    console.warn(
      `[e2e] Could not auto-create DB "${e2eDatabaseName}". Create it manually if missing.`,
    );
  }
}

/** Clears encrypted 2FA columns and bootstrap admin so API seed + login work with dev DataProtection keys. */
function resetAuthDataForE2e() {
  try {
    if (useDirectPostgresCli()) {
      resetE2eAuthData(e2eDatabaseName, adminEmail);
    } else {
      resetAuthDataForE2eDocker();
    }
    console.log(`[e2e] Reset auth data in "${e2eDatabaseName}" (2FA secrets + bootstrap admin).`);
  } catch {
    console.warn(
      `[e2e] Could not reset DB "${e2eDatabaseName}". If login fails with DataProtection errors, reset manually or set E2E_POSTGRES_CLI=1 with psql available.`,
    );
  }
}

export default async function globalSetup() {
  if (process.env.E2E_SKIP_WEBSERVER === '1') {
    console.log('[e2e] E2E_SKIP_WEBSERVER=1 — using externally managed servers.');
    return;
  }

  console.log(
    `[e2e] webServer mode: reuseExistingServer=${process.env.E2E_REUSE_SERVERS === 'true'}, db=${e2eDatabaseName}, postgresCli=${useDirectPostgresCli()}`,
  );

  const postgresUp = await canConnect(postgresHost, postgresPort);
  if (!postgresUp) {
    throw new Error(
      [
        `Postgres is not reachable at ${postgresHost}:${postgresPort}.`,
        'Start it before e2e:',
        '  docker compose -f local/docker-compose.yml up -d postgres',
      ].join('\n'),
    );
  }

  ensureE2eDatabaseExists();
  resetAuthDataForE2e();
}
