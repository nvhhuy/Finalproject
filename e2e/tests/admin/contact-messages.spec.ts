import { expect, test } from '@playwright/test';
import { waitForAdminRouteReady } from '../helpers/lazy';
import { loginAdminConsole } from '../helpers/login';

test.describe('Admin console — contact messages', () => {
  test.beforeEach(async ({ page }) => {
    await loginAdminConsole(page);
  });

  test('contact messages link is visible in the sidebar navigation', async ({ page }) => {
    await expect(
      page.getByRole('link', { name: /contact messages|tin nhắn liên hệ|联系消息/i }),
    ).toBeVisible({ timeout: 15_000 });
  });

  test('clicking the nav link navigates to /contact-messages', async ({ page }) => {
    await page.getByRole('link', { name: /contact messages|tin nhắn liên hệ|联系消息/i }).click();
    await expect(page).toHaveURL(/\/contact-messages/);
    await waitForAdminRouteReady(page);
  });

  test('contact messages page loads with the search bar and filter select', async ({ page }) => {
    await page.goto('/contact-messages');
    await waitForAdminRouteReady(page);

    await expect(
      page.getByPlaceholder(/search by name|search|tìm theo|搜索/i),
    ).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('select').first()).toBeVisible();
  });

  test('page shows empty state or a table with sender column header', async ({ page }) => {
    await page.goto('/contact-messages');
    await waitForAdminRouteReady(page);

    const senderHeader = page.getByRole('cell', { name: /sender|người gửi|发件人/i }).or(
      page.getByRole('columnheader', { name: /sender|người gửi|发件人/i }),
    );
    const emptyState = page.getByText(/no contact messages|chưa có|暂无/i);

    await expect(senderHeader.or(emptyState)).toBeVisible({ timeout: 15_000 });
  });

  test('detail panel shows placeholder when no message is selected', async ({ page }) => {
    await page.goto('/contact-messages');
    await waitForAdminRouteReady(page);

    await expect(
      page.getByText(/select a message|chọn.*tin nhắn|选择.*消息/i),
    ).toBeVisible({ timeout: 15_000 });
  });

  test('submitted contact message appears in the list and can be marked read', async ({ page, request }) => {
    // Submit a contact message directly via the API so we have something to find
    const uniqueEmail = `e2e-admin-${Date.now()}@test.local`;
    const apiBase = process.env.E2E_API_URL ?? 'http://localhost:5076';

    const submitResponse = await request.post(`${apiBase}/api/v1/contact`, {
      data: {
        name: 'E2E Admin Test',
        email: uniqueEmail,
        subject: 'Admin e2e visibility check',
        body: 'This was submitted by Playwright to verify admin visibility.',
      },
    });
    expect(submitResponse.ok()).toBeTruthy();

    await page.goto('/contact-messages');
    await waitForAdminRouteReady(page);

    // Search for the unique email so the row is visible
    await page.getByPlaceholder(/search by name|search|tìm theo|搜索/i).fill(uniqueEmail);
    await page.waitForTimeout(500); // debounce

    const row = page.getByText('E2E Admin Test').first();
    await expect(row).toBeVisible({ timeout: 15_000 });

    // Click the row to open the detail panel
    await row.click();

    // Detail panel should show the subject — use nth(1) because the subject also appears
    // in the table cell (nth 0); nth(1) targets the <p> inside the detail panel.
    await expect(page.getByText('Admin e2e visibility check').nth(1)).toBeVisible({ timeout: 10_000 });

    // Mark as read
    await page.getByRole('button', { name: /mark as read|đánh dấu đã đọc|标记为已读/i }).click();

    // Button should disappear (message is now read)
    await expect(
      page.getByRole('button', { name: /mark as read|đánh dấu đã đọc|标记为已读/i }),
    ).not.toBeVisible({ timeout: 10_000 });
  });
});
