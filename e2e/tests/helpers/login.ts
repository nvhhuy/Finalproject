import { expect, type Page } from '@playwright/test';

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@localhost.com';
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Temp!ChangeMe123';

/** Wait for login API response and surface failures in the test output. */
async function waitForLoginApi(page: Page) {
  const response = await page.waitForResponse(
    (r) => r.url().includes('/api/v1/auth/login') && r.request().method() === 'POST',
    { timeout: 30_000 },
  );
  if (!response.ok()) {
    const body = await response.text();
    throw new Error(`Login API failed (${response.status()}): ${body.slice(0, 500)}`);
  }
  return response;
}

export async function loginFrontend(page: Page, email = adminEmail, password = adminPassword) {
  await page.goto('/login');
  await page.locator('#email').fill(email);
  await page.locator('#password').fill(password);

  const loginApi = waitForLoginApi(page);
  const signIn = page.getByRole('button', { name: /sign in/i });
  await expect(signIn).toBeEnabled();
  await signIn.click();
  await loginApi;

  await expect(page).toHaveURL(/\/app/, { timeout: 30_000 });
}

export async function loginAdminConsole(page: Page, email = adminEmail, password = adminPassword) {
  await page.goto('/login');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/password/i).fill(password);

  const loginApi = waitForLoginApi(page);
  const signIn = page.getByRole('button', { name: /sign in/i });
  await expect(signIn).toBeEnabled();
  await signIn.click();
  await loginApi;

  await expect(page).not.toHaveURL(/\/login$/, { timeout: 30_000 });
}

export { adminEmail, adminPassword };
