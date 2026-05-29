import { expect, type Page } from '@playwright/test';
import { waitForFrontendAppShell } from './lazy';

/** Completes manual subscribe (starter) and lands on /app. */
export async function subscribeStarterPlan(page: Page, displayName = 'E2E Test Org') {
  const slug = `e2e-${Date.now().toString(36)}`;
  await page.goto('/app/subscribe?plan=starter');
  await page.getByPlaceholder(/slug/i).fill(slug);
  await page.getByPlaceholder(/display name|tenant name/i).fill(displayName);
  const submit = page.getByRole('button', { name: /create tenant|subscribe|submit/i });
  await expect(submit).toBeEnabled();
  await submit.click();
  await expect(page).toHaveURL(/\/app/, { timeout: 30_000 });
  await waitForFrontendAppShell(page);
  return slug;
}
