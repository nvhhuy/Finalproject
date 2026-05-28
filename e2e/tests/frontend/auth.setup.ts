import { test as setup } from '@playwright/test';
import { loginFrontend } from '../helpers/login';

const authFile = 'playwright/.auth/frontend.json';

setup('authenticate', async ({ page }) => {
  await loginFrontend(page);
  await page.context().storageState({ path: authFile });
});
