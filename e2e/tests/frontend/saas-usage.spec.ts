import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell } from '../helpers/lazy';
import { loginFrontend } from '../helpers/login';
import { subscribeStarterPlan } from '../helpers/saas';

test.describe('Frontend — SaaS usage (tenant & wallet)', () => {
  test('organization page shows subscription after subscribe', async ({ page }) => {
    await loginFrontend(page);
    await subscribeStarterPlan(page, 'E2E Org Usage');

    await page.getByRole('link', { name: /organization/i }).click();
    await expect(page).toHaveURL(/\/app\/tenant/);
    await waitForFrontendAppShell(page);

    await expect(page.getByRole('heading', { name: /organization usage/i })).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByText(/starter|trialing|subscription/i).first()).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByText(/single sign-on|sso/i).first()).toBeVisible();
    await expect(page.getByText(/domains|workspace url/i).first()).toBeVisible();
  });

  test('wallet page loads and manual top-up increases balance', async ({ page }) => {
    await loginFrontend(page);
    await page.goto('/app/wallet');
    await waitForFrontendAppShell(page);

    await expect(page.getByRole('heading', { name: /personal wallet/i })).toBeVisible();
    const topUpResponse = page.waitForResponse(
      (r) => r.url().includes('/api/v1/wallet/top-up') && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: /top up/i }).click();
    const response = await topUpResponse;
    expect(response.ok()).toBeTruthy();

    await expect(page.locator('.text-3xl.font-semibold')).toContainText(/[1-9]\d*/, {
      timeout: 15_000,
    });
  });
});

test.describe('Frontend — tenant public landing', () => {
  test('default organization is readable without auth', async ({ page }) => {
    await page.goto('/t/default');
    await expect(page.getByRole('heading', { name: /default organization/i })).toBeVisible({
      timeout: 30_000,
    });
    await expect(page.getByRole('link', { name: /open workspace|workspace/i }).first()).toBeVisible();
  });
});
