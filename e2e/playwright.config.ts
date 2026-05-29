import path from 'node:path';
import { defineConfig, devices } from '@playwright/test';

const repoRoot = path.resolve(process.cwd(), '..');

// API: 5076 avoids macOS AirPlay on :5000. Override with E2E_API_URL=http://localhost:5000 if free.
const apiUrl = process.env.E2E_API_URL ?? 'http://localhost:5076';
const frontendUrl = process.env.E2E_FRONTEND_URL ?? 'http://localhost:3000';
const adminUrl = process.env.E2E_ADMIN_URL ?? 'http://localhost:5174';
const dataProtectionKeys = path.join(repoRoot, 'local/dataprotection-keys');
const e2eDatabaseUrl =
  process.env.E2E_DATABASE_URL ??
  'Host=localhost;Port=5432;Database=baseproject_e2e;Username=postgres;Password=postgres';

const webServerTimeout = Number(process.env.E2E_WEBSERVER_TIMEOUT_MS ?? '300000');
const reuseExistingServer = process.env.E2E_REUSE_SERVERS === 'true';

export default defineConfig({
  testDir: './tests',
  globalSetup: './global-setup.ts',
  globalTeardown: './global-teardown.ts',
  fullyParallel: process.env.E2E_STABLE_PARALLEL === 'true',
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: (() => {
    const fromEnv = process.env.E2E_WORKERS;
    if (fromEnv) return Number.parseInt(fromEnv, 10);
    return process.env.CI ? 1 : 1;
  })(),
  reporter: [['list'], ['html', { open: 'never' }]],
  timeout: 60_000,
  use: {
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'frontend-setup',
      testMatch: /frontend\/auth\.setup\.ts/,
      use: { ...devices['Desktop Chrome'], baseURL: frontendUrl },
    },
    {
      name: 'frontend',
      testDir: './tests/frontend',
      testIgnore: /auth\.setup\.ts/,
      dependencies: ['frontend-setup'],
      use: { ...devices['Desktop Chrome'], baseURL: frontendUrl },
    },
    {
      name: 'admin',
      testDir: './tests/admin',
      dependencies: ['frontend-setup'],
      use: { ...devices['Desktop Chrome'], baseURL: adminUrl },
    },
  ],
  webServer: process.env.E2E_SKIP_WEBSERVER
    ? undefined
    : [
        {
          command:
            'dotnet run --project ../src/Backend/Api/Api.csproj --no-launch-browser --no-launch-profile',
          url: `${apiUrl}/health/live`,
          reuseExistingServer,
          timeout: webServerTimeout,
          stdout: 'pipe',
          stderr: 'pipe',
          env: {
            ASPNETCORE_ENVIRONMENT: 'Development',
            ASPNETCORE_URLS: apiUrl,
            DataProtection__KeysDirectory: dataProtectionKeys,
            ConnectionStrings__DatabaseConnectionString: e2eDatabaseUrl,
            ConnectionStrings__DatabaseReadConnectionString: e2eDatabaseUrl,
          },
        },
        {
          command: 'npm run dev',
          cwd: '../src/Frontend',
          url: frontendUrl,
          reuseExistingServer,
          timeout: webServerTimeout,
          stdout: 'pipe',
          stderr: 'pipe',
          env: {
            API_PROXY_TARGET: apiUrl,
            NODE_TLS_REJECT_UNAUTHORIZED: '0',
          },
        },
        {
          command: 'npm run dev',
          cwd: '../src/Admin',
          url: adminUrl,
          reuseExistingServer,
          timeout: webServerTimeout,
          stdout: 'pipe',
          stderr: 'pipe',
          env: {
            VITE_API_PROXY_TARGET: apiUrl,
          },
        },
      ],
});
