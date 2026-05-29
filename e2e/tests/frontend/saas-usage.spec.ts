import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell } from '../helpers/lazy';
import { loginFrontend } from '../helpers/login';
import { subscribeStarterPlan, waitForPublicTenant } from '../helpers/saas';

test.describe('Frontend — SaaS usage (tenant & wallet)', () => {
  test('organization page shows subscription after subscribe', async ({ page }) => {
    await loginFrontend(page);
    await subscribeStarterPlan(page, 'E2E Org Usage');

    await page.goto('/app/tenant');
    await expect(page).toHaveURL(/\/app\/tenant/);
    await waitForFrontendAppShell(page);

    await expect(page.getByRole('heading', { name: /organization usage/i })).toBeVisible({
      timeout: 15_000,
    });

    await expect(page.getByRole('heading', { name: /^subscription$/i })).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByText(/starter/i).first()).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/trialing/i).first()).toBeVisible({ timeout: 15_000 });

    const ssoHeading = page.getByRole('heading', { name: /single sign-on/i });
    await ssoHeading.scrollIntoViewIfNeeded();
    await expect(ssoHeading).toBeVisible({ timeout: 15_000 });

    const domainsHeading = page.getByRole('heading', { name: /domains & workspace url/i });
    await domainsHeading.scrollIntoViewIfNeeded();
    await expect(domainsHeading).toBeVisible({ timeout: 15_000 });
  });

  test('wallet page loads and manual top-up increases balance', async ({ page }) => {
    await loginFrontend(page);
    await page.goto('/app/wallet');
    await waitForFrontendAppShell(page);

    await expect(page.getByRole('heading', { name: /personal wallet/i })).toBeVisible();
    await page.waitForResponse(
      (r) => r.url().includes('/api/v1/wallet/balance') && r.request().method() === 'GET' && r.ok(),
      { timeout: 30_000 },
    );

    const topUpResponse = page.waitForResponse(
      (r) => r.url().includes('/api/v1/wallet/top-up') && r.request().method() === 'POST',
    );
    await page.getByRole('button', { name: /top up/i }).click();
    const response = await topUpResponse;
    expect(response.ok(), `top-up failed (${response.status()}): ${await response.text()}`).toBeTruthy();

    await expect(page.locator('.text-3xl.font-semibold')).toContainText(/[1-9]\d*/, {
      timeout: 15_000,
    });
  });
});

test.describe('Frontend — tenant public landing', () => {
  test('default organization is readable without auth', async ({ page }) => {
    const tenantLoaded = waitForPublicTenant(page, 'default');
    await page.goto('/t/default');
    await tenantLoaded;

    await expect(page.getByRole('heading', { name: /default organization/i })).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByRole('link', { name: /open workspace|workspace/i }).first()).toBeVisible();
  });
});
