import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell, waitForFrontendLanding } from '../helpers/lazy';
import { loginFrontend } from '../helpers/login';

test.describe('Frontend — public auth', () => {
  test('landing page links to login and register', async ({ page }) => {
    await page.goto('/');
    await waitForFrontendLanding(page);
  });

  test('login with seeded admin reaches app dashboard', async ({ page }) => {
    await loginFrontend(page);
    await expect(page.getByRole('navigation')).toBeVisible();
  });

  test('register page renders', async ({ page }) => {
    await page.goto('/register');
    await expect(page.getByRole('button', { name: /register|create|sign up/i })).toBeVisible();
  });

  test('forgot password page renders', async ({ page }) => {
    await page.goto('/forgot-password');
    await expect(page.getByRole('button', { name: /send|reset|submit/i })).toBeVisible();
  });
});

test.describe('Frontend — authenticated app', () => {
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

  test('support center create ticket flow works', async ({ page }) => {
    await page.goto('/app/support');
    await waitForFrontendAppShell(page);
    await expect(page.getByText(/support|ticket/i).first()).toBeVisible();

    await page.getByPlaceholder(/subject|tiêu đề|主题/i).fill(`E2E ticket ${Date.now()}`);
    await page.getByPlaceholder(/describe|mô tả|描述/i).fill('Support request created by Playwright test.');
    await page.getByRole('button', { name: /create ticket|tạo ticket|创建工单/i }).click();

    await expect(page.locator('text=/SUP-\\d{8}-\\d{5}/').first()).toBeVisible({ timeout: 15_000 });
  });
});
