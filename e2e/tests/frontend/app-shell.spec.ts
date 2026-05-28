import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell } from '../helpers/lazy';

test.describe('Frontend — authenticated app shell', () => {
  test.use({ storageState: 'playwright/.auth/frontend.json' });

  test('dashboard and profile navigation', async ({ page }) => {
    await page.goto('/app');
    await waitForFrontendAppShell(page);
    await expect(page.getByText(/workspace|dashboard/i).first()).toBeVisible();
    await page.getByRole('link', { name: /profile/i }).first().click();
    await expect(page).toHaveURL(/\/app\/profile/);
    await expect(page.locator('#firstName').first()).toBeVisible();
  });

  test('2FA setup page loads', async ({ page }) => {
    await page.goto('/app/security/2fa');
    await waitForFrontendAppShell(page);
    await expect(page.getByRole('button', { name: /begin|enable|setup/i }).first()).toBeVisible();
  });

  test('media lab page loads', async ({ page }) => {
    await page.goto('/app/media');
    await waitForFrontendAppShell(page);
    await expect(page.getByText(/media|upload/i).first()).toBeVisible();
  });
});
