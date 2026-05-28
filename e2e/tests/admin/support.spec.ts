import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — support tickets', () => {
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('support tickets page loads with key controls', async ({ page }) => {
    await page.getByRole('link', { name: /support tickets|ticket hỗ trợ|支持工单/i }).click();
    await expect(page).toHaveURL(/\/support/);
    await waitForAdminRouteReady(page);
    await expect(page.getByText(/support tickets|ticket hỗ trợ|支持工单/i).first()).toBeVisible();
    await expect(page.getByPlaceholder(/search by ticket|search by|tìm theo|按工单号/i)).toBeVisible();
    await expect(page.locator('select').first()).toBeVisible();
    await expect(page.getByText(/select a ticket|chọn ticket|选择工单/i).first()).toBeVisible();
  });
});
