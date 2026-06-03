import { expect, test } from '@playwright/test';
import { waitForFrontendLanding } from '../helpers/lazy';

test.describe('Frontend — pricing & SaaS marketing', () => {
  test('pricing page loads plan cards from API', async ({ page }) => {
    await page.goto('/pricing');
    await expect(page.getByRole('heading', { name: /plan|pricing/i })).toBeVisible({ timeout: 30_000 });
    await expect(
      page
        .getByRole('link', { name: /get started|choose|bắt đầu|开始使用/i })
        .or(page.getByRole('button', { name: /get started|choose|bắt đầu|开始使用/i }))
        .first(),
    ).toBeVisible({
      timeout: 30_000,
    });
  });

  test('about and contact pages render', async ({ page }) => {
    await page.goto('/about');
    await expect(page.getByRole('heading').first()).toBeVisible({ timeout: 30_000 });
    await page.goto('/contact');
    await expect(page.getByRole('heading').first()).toBeVisible({ timeout: 30_000 });
  });

  test('landing links to pricing', async ({ page }) => {
    await page.goto('/');
    await waitForFrontendLanding(page);
    await page.getByRole('link', { name: /pricing/i }).click();
    await expect(page).toHaveURL(/\/pricing/);
  });
});
