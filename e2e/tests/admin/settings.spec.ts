import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — settings', () => {
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('users list and settings backup controls work', async ({ page }) => {
    await page.getByRole('link', { name: /^users$/i }).click();
    await expect(page).toHaveURL(/\/users/);
    await waitForAdminRouteReady(page);
    await expect(page.getByRole('table').or(page.getByRole('heading', { name: /users/i })).first()).toBeVisible({
      timeout: 15_000,
    });

    await page.getByRole('link', { name: /settings/i }).click();
    await expect(page).toHaveURL(/\/settings/);
    await waitForAdminRouteReady(page);
    await expect(page.getByText(/public registration|allow/i).first()).toBeVisible();
    await expect(page.getByText(/system backup|sao lưu hệ thống|系统备份/i).first()).toBeVisible();
    await expect(page.getByText(/database backup|sao lưu cơ sở dữ liệu|数据库备份/i).first()).toBeVisible();

    const scheduledDbToggle = page
      .locator('label')
      .filter({ hasText: /enable scheduled database backup|bật sao lưu db định kỳ|启用定时数据库备份/i })
      .locator('input[type="checkbox"]');
    await expect(scheduledDbToggle).toBeVisible();
    await expect(scheduledDbToggle).toBeEnabled();

    const publicRegToggle = page
      .locator('label')
      .filter({ hasText: /enabled|disabled|bật|tắt|已启用|已禁用/i })
      .first()
      .locator('input[type="checkbox"]');
    const before = await publicRegToggle.isChecked();
    await publicRegToggle.click();
    await expect(publicRegToggle).toBeChecked({ checked: !before });
    await publicRegToggle.click();
    await expect(publicRegToggle).toBeChecked({ checked: before });

    const dbRetentionInput = page.locator('input[type="number"]').last();
    await expect(dbRetentionInput).toBeVisible();
    await dbRetentionInput.fill('8');
    await expect(dbRetentionInput).toHaveValue('8');
  });
});
