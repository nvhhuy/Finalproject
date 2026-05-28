import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — public', () => {
  test('login page renders', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: /admin sign in/i })).toBeVisible();
  });

  test('login reaches dashboard', async ({ page }) => {
    await loginAdminConsole(page);
    await expect(page.locator('nav')).toBeVisible();
  });
});

test.describe('Admin console — authenticated', () => {
  // Admin keeps accessToken in Zustand memory only — Playwright storageState cannot restore it.
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('dashboard shows metrics', async ({ page }) => {
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.getByText(/active|user|registration|dashboard/i).first()).toBeVisible({
      timeout: 15_000,
    });
  });

  test('users list and settings', async ({ page }) => {
    await page.getByRole('link', { name: /^users$/i }).click();
    await expect(page).toHaveURL(/\/users/);
    await waitForAdminRouteReady(page);
    await expect(page.getByRole('table').or(page.getByRole('heading', { name: /users/i })).first()).toBeVisible({
      timeout: 15_000,
    });

    await page.getByRole('link', { name: /settings/i }).click();
    await expect(page).toHaveURL(/\/settings/);
    await waitForAdminRouteReady(page);
    await expect(page.getByText(/public registration|allow/i).first()).toBeVisible();
  });

  test('audit logs and DLQ pages load', async ({ page }) => {
    await page.getByRole('link', { name: /audit/i }).click();
    await expect(page).toHaveURL(/audit-logs/);
    await waitForAdminRouteReady(page);

    await page.getByRole('link', { name: /dlq/i }).click();
    await expect(page).toHaveURL(/messaging/);
    await waitForAdminRouteReady(page);
    await expect(page.getByRole('button', { name: /peek|replay|drain/i }).first()).toBeVisible();
  });
});
