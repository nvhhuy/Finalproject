import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — audit export', () => {
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('export CSV calls audit export API', async ({ page }) => {
    await page.getByRole('link', { name: /audit/i }).click();
    await expect(page).toHaveURL(/audit-logs/);
    await waitForAdminRouteReady(page);

    const exportResponse = page.waitForResponse(
      (r) => r.url().includes('/api/v1/admin/audit-logs/export') && r.request().method() === 'GET',
    );
    await page.getByRole('button', { name: /export csv/i }).click();
    const response = await exportResponse;
    expect(response.ok()).toBeTruthy();
    expect(response.headers()['content-type'] ?? '').toMatch(/text\/csv/);
  });
});
