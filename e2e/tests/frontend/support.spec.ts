import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell } from '../helpers/lazy';

test.describe('Frontend — support center', () => {
  test.use({ storageState: 'playwright/.auth/frontend.json' });

  test('support center create ticket flow works', async ({ page }) => {
    await page.goto('/app/support');
    await waitForFrontendAppShell(page);
    await expect(page.getByText(/support|ticket/i).first()).toBeVisible();

    await page.getByPlaceholder(/subject|tiêu đề|主题/i).fill(`E2E ticket ${Date.now()}`);
    await page.getByPlaceholder(/describe|mô tả|描述/i).fill('Support request created by Playwright test.');
    await page.getByRole('button', { name: /create ticket|tạo ticket|创建工单/i }).click();

    await expect(page.locator('text=/SUP-\\d{8}-\\d{5}/').first()).toBeVisible({ timeout: 15_000 });
  });

  test('support center can filter and reply on created ticket', async ({ page }) => {
    await page.goto('/app/support');
    await waitForFrontendAppShell(page);

    const subject = `E2E follow-up ${Date.now()}`;
    const replyBody = `E2E reply ${Date.now()}`;

    await page.getByPlaceholder(/subject|tiêu đề|主题/i).fill(subject);
    await page.getByPlaceholder(/describe|mô tả|描述/i).fill('Ticket used for e2e filter and reply flow.');
    await page.getByRole('button', { name: /create ticket|tạo ticket|创建工单/i }).click();

    const createdTicket = page.locator('text=/SUP-\\d{8}-\\d{5}/').first();
    await expect(createdTicket).toBeVisible({ timeout: 15_000 });
    const ticketNumber = (await createdTicket.textContent())?.trim() ?? '';
    expect(ticketNumber).toMatch(/^SUP-\d{8}-\d{5}$/);

    await page.getByPlaceholder(/search by ticket|search|tìm theo mã|按工单号/i).fill(ticketNumber);
    await page.getByRole('button', { name: /submit|search|tìm|查询/i }).click();
    await expect(page.getByRole('button', { name: new RegExp(ticketNumber) }).first()).toBeVisible({ timeout: 15_000 });

    await page.getByPlaceholder(/reply|phản hồi|回复/i).fill(replyBody);
    await page.getByRole('button', { name: /^reply$|send reply|gửi phản hồi|发送回复/i }).click();
    await expect(page.getByText(replyBody)).toBeVisible({ timeout: 15_000 });
  });
});
