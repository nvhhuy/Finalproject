import { expect, type Page } from '@playwright/test';
import { waitForFrontendAppShell } from './lazy';

/** Returns a promise that resolves when tenant entitlements API loads. Register before navigation. */
export function waitForTenantEntitlements(page: Page) {
  return page.waitForResponse(
    (r) => r.url().includes('/entitlements') && r.request().method() === 'GET' && r.ok(),
    { timeout: 30_000 },
  );
}

/** Returns a promise that resolves when public tenant metadata loads. Register before navigation. */
export function waitForPublicTenant(page: Page, slug: string) {
  return page.waitForResponse(
    (r) =>
      r.url().includes(`/api/v1/public/tenants/${encodeURIComponent(slug)}`) &&
      r.request().method() === 'GET' &&
      r.ok(),
    { timeout: 30_000 },
  );
}

/** Completes manual subscribe (starter) and lands on /app. */
export async function subscribeStarterPlan(page: Page, displayName = 'E2E Test Org') {
  const slug = `e2e-${Date.now().toString(36)}`;
  await page.goto('/app/subscribe?plan=starter');
  await page.getByPlaceholder(/slug/i).fill(slug);
  await page.getByPlaceholder(/display name|tenant name/i).fill(displayName);
  const submit = page.getByRole('button', { name: /create tenant|subscribe|submit/i });
  await expect(submit).toBeEnabled();
  const subscribeApi = page.waitForResponse(
    (r) => r.url().includes('/api/v1/billing/subscribe') && r.request().method() === 'POST',
  );
  await submit.click();
  const response = await subscribeApi;
  expect(response.ok(), `subscribe failed (${response.status()}): ${await response.text()}`).toBeTruthy();
  await expect(page).toHaveURL(/\/app/, { timeout: 30_000 });
  await waitForFrontendAppShell(page);
  return { slug };
}
