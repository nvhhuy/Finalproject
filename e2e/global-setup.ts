import { execSync } from 'node:child_process';
import net from 'node:net';
import path from 'node:path';

const repoRoot = path.resolve(process.cwd(), '..');

const postgresHost = process.env.E2E_POSTGRES_HOST ?? 'localhost';
const postgresPort = Number(process.env.E2E_POSTGRES_PORT ?? '5432');
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@localhost.com';

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
  const sql =
    `UPDATE "Users" SET "TwoFactorSecret" = NULL; DELETE FROM "Users" WHERE "Email" = '${email}';`;

  try {
    execSync(
      `docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d finalproject -v ON_ERROR_STOP=1 -c "${sql}"`,
      { cwd: repoRoot, stdio: 'pipe' },
    );
    console.log('[e2e] Reset auth data (2FA secrets + bootstrap admin) for fresh seed.');
  } catch {
    console.warn(
      '[e2e] Could not reset DB via docker compose. If login fails with DataProtection errors, run:\n' +
        `  docker compose -f local/docker-compose.yml exec -T postgres psql -U postgres -d finalproject -c "UPDATE \\"Users\\" SET \\"TwoFactorSecret\\" = NULL; DELETE FROM \\"Users\\" WHERE \\"Email\\" = '${adminEmail}';"`,
    );
  }
}

export default async function globalSetup() {
  if (process.env.E2E_SKIP_WEBSERVER === '1') return;

  const postgresUp = await canConnect(postgresHost, postgresPort);
  if (!postgresUp) {
    throw new Error(
      [
        `Postgres is not reachable at ${postgresHost}:${postgresPort}.`,
        'Start it before e2e:',
        '  docker compose -f local/docker-compose.yml up -d postgres',
        '  ./tools/backend/migrate.sh',
      ].join('\n'),
    );
  }

  resetAuthDataForE2e();
}
