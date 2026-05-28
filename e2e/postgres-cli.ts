import { execSync } from 'node:child_process';

export function resolveE2eDatabaseName(
  defaultName: string,
  env: NodeJS.ProcessEnv = process.env,
): string {
  const fromConnString = env.E2E_DATABASE_URL?.split(';')
    .map((part) => part.trim())
    .find((part) => part.toLowerCase().startsWith('database='))
    ?.split('=')[1];

  return env.E2E_DATABASE_NAME ?? fromConnString ?? defaultName;
}

export function useDirectPostgresCli(env: NodeJS.ProcessEnv = process.env): boolean {
  return env.E2E_POSTGRES_CLI === '1' || env.CI === 'true' || env.CI === '1';
}

function pgConnection(env: NodeJS.ProcessEnv = process.env) {
  return {
    host: env.E2E_POSTGRES_HOST ?? 'localhost',
    port: env.E2E_POSTGRES_PORT ?? '5432',
    user: env.E2E_POSTGRES_USER ?? 'postgres',
    password: env.E2E_POSTGRES_PASSWORD ?? 'postgres',
  };
}

function pgExec(command: string, env: NodeJS.ProcessEnv = process.env) {
  const { password } = pgConnection(env);
  execSync(command, {
    stdio: 'pipe',
    env: { ...process.env, PGPASSWORD: password },
  });
}

export function ensureDatabaseExists(databaseName: string, env: NodeJS.ProcessEnv = process.env) {
  const { host, port, user } = pgConnection(env);
  pgExec(
    `createdb -h ${host} -p ${port} -U ${user} "${databaseName}" >/dev/null 2>&1 || true`,
    env,
  );
}

export function dropDatabaseIfExists(databaseName: string, env: NodeJS.ProcessEnv = process.env) {
  const { host, port, user } = pgConnection(env);
  pgExec(`dropdb -h ${host} -p ${port} -U ${user} --if-exists --force "${databaseName}"`, env);
}

export function resetE2eAuthData(
  databaseName: string,
  adminEmail: string,
  env: NodeJS.ProcessEnv = process.env,
) {
  const email = adminEmail.replace(/'/g, "''");
  const db = databaseName.replace(/'/g, "''");
  const sql = `UPDATE "Users" SET "TwoFactorSecret" = NULL; DELETE FROM "Users" WHERE "Email" = '${email}';`;
  const { host, port, user } = pgConnection(env);
  pgExec(
    `psql -h ${host} -p ${port} -U ${user} -d "${db}" -v ON_ERROR_STOP=1 -c "${sql}"`,
    env,
  );
}
