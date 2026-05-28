import { expect, test } from '@playwright/test';
import { waitForFrontendLanding } from '../helpers/lazy';
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
