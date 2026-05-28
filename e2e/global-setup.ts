import { execSync } from 'node:child_process';
import net from 'node:net';
import path from 'node:path';

const repoRoot = path.resolve(process.cwd(), '..');

const postgresHost = process.env.E2E_POSTGRES_HOST ?? 'localhost';
const postgresPort = Number(process.env.E2E_POSTGRES_PORT ?? '5432');
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@localhost.com';
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

/** Clears encrypted 2FA columns and bootstrap admin so API seed + login work with dev DataProtection keys. */
function resetAuthDataForE2e() {
  const email = adminEmail.replace(/'/g, "''");
  const databaseName = e2eDatabaseName.replace(/'/g, "''");
  const sql =
    `UPDATE "Users" SET "TwoFactorSecret" = NULL; DELETE FROM "Users" WHERE "Email" = '${email}';`;

  try {
    execSync(
      `docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d "${databaseName}" -v ON_ERROR_STOP=1 -c "${sql}"`,
      { cwd: repoRoot, stdio: 'pipe' },
    );
    console.log(`[e2e] Reset auth data in "${databaseName}" (2FA secrets + bootstrap admin).`);
  } catch {
    console.warn(
      `[e2e] Could not reset DB "${e2eDatabaseName}" via docker compose. If login fails with DataProtection errors, run:\n` +
        `  docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d ${e2eDatabaseName} -c "UPDATE \\"Users\\" SET \\"TwoFactorSecret\\" = NULL; DELETE FROM \\"Users\\" WHERE \\"Email\\" = '${adminEmail}';"`,
    );
  }
}

/** Ensures Playwright uses a dedicated DB to avoid polluting local dev data. */
function ensureE2eDatabaseExists() {
  const databaseName = e2eDatabaseName.replace(/'/g, "''");
  try {
    execSync(
      `docker compose -f local/docker-compose.yml exec -T postgres sh -lc "createdb -U postgres '${databaseName}' >/dev/null 2>&1 || true"`,
      { cwd: repoRoot, stdio: 'pipe' },
    );
    console.log(`[e2e] Ensured dedicated database "${databaseName}" exists.`);
  } catch {
    console.warn(
      `[e2e] Could not auto-create DB "${e2eDatabaseName}". Create it manually if missing:\n` +
        `  docker compose -f local/docker-compose.yml exec -T postgres createdb -U postgres ${e2eDatabaseName}`,
    );
  }
}

export default async function globalSetup() {
  if (process.env.E2E_SKIP_WEBSERVER === '1') {
    console.log('[e2e] E2E_SKIP_WEBSERVER=1 — using externally managed servers.');
    return;
  }

  console.log(
    `[e2e] webServer mode: reuseExistingServer=${process.env.E2E_REUSE_SERVERS === 'true'}, db=${e2eDatabaseName}`,
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
