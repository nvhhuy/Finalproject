import { expect, test } from '@playwright/test';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — public auth', () => {
  test('login page renders', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('heading', { name: /admin sign in/i })).toBeVisible();
  });

  test('login reaches dashboard', async ({ page }) => {
    await loginAdminConsole(page);
    await expect(page.locator('nav')).toBeVisible();
  });
});
