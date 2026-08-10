import { expect, test } from '@playwright/test';
import { waitForFrontendAppShell } from '../helpers/lazy';

test.describe('Frontend — books CRUD', () => {
  test.use({ storageState: 'playwright/.auth/frontend.json' });

  // Anonymous rejection is covered at the API layer (BooksIntegrationTests.Anonymous_request_is_rejected)
  // and by the shared RequireAuth guard already exercised by every other /app/* page.

  test('create, edit, and delete a book', async ({ page }) => {
    await page.goto('/app/books');
    await waitForFrontendAppShell(page);
    await expect(page.getByText(/my books|sách của tôi|我的书籍/i).first()).toBeVisible();

    const title = `E2E Book ${Date.now()}`;
    const updatedTitle = `${title} (revised)`;

    await page.getByPlaceholder(/^title$|tiêu đề|标题/i).fill(title);
    await page.getByPlaceholder(/^author$|tác giả|作者/i).fill('Playwright Author');
    await page.getByRole('button', { name: /add book|thêm sách|添加书籍/i }).click();

    const bookRow = page.locator('li', { hasText: title }).first();
    await expect(bookRow).toBeVisible({ timeout: 15_000 });

    await bookRow.getByRole('button', { name: /^edit$|sửa|编辑/i }).click();
    await page.getByPlaceholder(/^title$|tiêu đề|标题/i).fill(updatedTitle);
    await page.getByRole('button', { name: /save changes|lưu thay đổi|保存修改/i }).click();

    const updatedRow = page.locator('li', { hasText: updatedTitle }).first();
    await expect(updatedRow).toBeVisible({ timeout: 15_000 });

    await updatedRow.getByRole('button', { name: /^delete$|xóa|删除/i }).click();
    await expect(page.locator('li', { hasText: updatedTitle })).toHaveCount(0, { timeout: 15_000 });
  });
});
