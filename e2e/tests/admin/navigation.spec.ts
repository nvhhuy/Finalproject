import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — navigation', () => {
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('dashboard shows metrics', async ({ page }) => {
    await expect(page.locator('nav')).toBeVisible();
    await expect(page.getByText(/active|user|registration|dashboard/i).first()).toBeVisible({
      timeout: 15_000,
    });
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
