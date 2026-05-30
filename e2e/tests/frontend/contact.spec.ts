import { expect, test } from '@playwright/test';

test.describe('Frontend — contact page (public)', () => {
  test('contact page is accessible without authentication', async ({ page }) => {
    await page.goto('/contact');
    await expect(page.getByRole('heading', { name: /contact/i })).toBeVisible({ timeout: 15_000 });
  });

  test('contact form renders all required fields', async ({ page }) => {
    await page.goto('/contact');

    await expect(page.getByLabel(/your name|name|họ và tên|姓名/i)).toBeVisible({ timeout: 15_000 });
    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/subject|tiêu đề|主题/i)).toBeVisible();
    await expect(page.getByLabel(/message|tin nhắn|消息/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /send|gửi|发送/i })).toBeVisible();
  });

  test('contact form submits successfully and shows success state', async ({ page }) => {
    await page.goto('/contact');

    await page.getByLabel(/your name|name|họ và tên|姓名/i).fill('Playwright Test User');
    await page.getByLabel(/email/i).fill('playwright-contact@test.local');
    await page.getByLabel(/subject|tiêu đề|主题/i).fill('E2E contact test');
    await page.getByLabel(/message|tin nhắn|消息/i).fill(
      'This is an automated e2e test submission. Please ignore.',
    );

    const [submitResponse] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/api/v1/contact') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: /send|gửi|发送/i }).click(),
    ]);
    expect(submitResponse.ok()).toBeTruthy();

    await expect(
      page.getByText(/message sent|đã gửi|消息已发送/i).or(page.getByText(/thank|cảm ơn|感谢/i)).first(),
    ).toBeVisible({ timeout: 15_000 });
  });

  test('send another link resets the form after successful submission', async ({ page }) => {
    await page.goto('/contact');

    await page.getByLabel(/your name|name|họ và tên|姓名/i).fill('Reset Test User');
    await page.getByLabel(/email/i).fill('reset@test.local');
    await page.getByLabel(/subject|tiêu đề|主题/i).fill('Reset form test');
    await page.getByLabel(/message|tin nhắn|消息/i).fill('Testing the reset flow.');

    await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/api/v1/contact') && r.request().method() === 'POST',
      ),
      page.getByRole('button', { name: /send|gửi|发送/i }).click(),
    ]);

    // Success state shown
    await expect(
      page.getByText(/message sent|đã gửi|消息已发送/i).or(page.getByText(/thank|cảm ơn|感谢/i)).first(),
    ).toBeVisible({ timeout: 15_000 });

    // Click "Send another" to reset
    await page.getByRole('button', { name: /send another|another|gửi.*khác|发送另/i }).click();

    // Form should be visible again
    await expect(page.getByLabel(/your name|name|họ và tên|姓名/i)).toBeVisible({ timeout: 10_000 });
  });
});
