import { test } from '@playwright/test';
import { loginFrontend } from '../helpers/login';
import { subscribeStarterPlan } from '../helpers/saas';

test.describe('Frontend — subscribe flow', () => {
  test('authenticated user can open subscribe form and submit', async ({ page }) => {
    await loginFrontend(page);
    await subscribeStarterPlan(page);
  });
});
