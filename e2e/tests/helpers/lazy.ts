import { expect, type Page } from '@playwright/test';

/**
 * Next.js `dynamic()` shells (AppShellSkeleton, LandingSkeleton) render before the real layout.
 * Wait for interactive chrome, not pulse placeholders.
 */
export async function waitForFrontendAppShell(page: Page) {
  const nav = page.getByRole('navigation').first();
  await expect(nav).toBeVisible({ timeout: 30_000 });
  await expect(page.getByRole('link', { name: /profile|dashboard|media|2fa/i }).first()).toBeVisible({
    timeout: 30_000,
  });
}

/** Marketing landing lazy-loads PublicShell after LandingSkeleton. */
export async function waitForFrontendLanding(page: Page) {
  await expect(page.getByRole('link', { name: /get started|register|sign up/i }).first()).toBeVisible({
    timeout: 30_000,
  });
  await expect(page.getByRole('link', { name: /login|sign in/i }).first()).toBeVisible({
    timeout: 30_000,
  });
}

/** Admin routes use React.lazy + Suspense (RouteFallback with role=status). */
export async function waitForAdminRouteReady(page: Page) {
  await expect(page.locator('[role="status"][aria-busy="true"]')).toHaveCount(0, { timeout: 20_000 });
}
