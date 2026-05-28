import { expect, test } from '@playwright/test';
import { loginFrontend } from '../helpers/login';

test.describe('Frontend — public auth', () => {
  test('landing page links to login and register', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('link', { name: /get started|register/i }).first()).toBeVisible();
    await expect(page.getByRole('link', { name: /login/i }).first()).toBeVisible();
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
    await expect(page.getByText(/workspace|dashboard/i).first()).toBeVisible();
    await page.getByRole('link', { name: /profile/i }).first().click();
    await expect(page).toHaveURL(/\/app\/profile/);
    await expect(page.locator('#firstName').first()).toBeVisible();
  });

  test('2FA setup page loads', async ({ page }) => {
    await page.goto('/app/security/2fa');
    await expect(page.getByRole('button', { name: /begin|enable|setup/i }).first()).toBeVisible();
  });

  test('media lab page loads', async ({ page }) => {
    await page.goto('/app/media');
    await expect(page.getByText(/media|upload/i).first()).toBeVisible();
  });
});
