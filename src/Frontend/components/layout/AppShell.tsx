"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useTranslation } from "react-i18next";
import { AppBrand } from "@/components/layout/AppBrand";
import { UserMenu } from "@/components/layout/UserMenu";
import { cn } from "@/lib/ui/cn";

export function AppShell({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  const pathname = usePathname();
  const navItems = [
    { href: "/app", label: t("nav.dashboard") },
    { href: "/app/profile", label: t("nav.profile") },
    { href: "/app/security/2fa", label: t("nav.twoFa") },
    { href: "/app/media", label: t("nav.mediaLab") },
    { href: "/app/support", label: t("nav.support") },
    { href: "/app/notifications", label: t("nav.notifications") },
    { href: "/app/wallet", label: t("nav.wallet") },
    { href: "/app/subscribe", label: t("nav.subscribe") },
    { href: "/app/tenant", label: t("nav.tenant") },
  ];

  return (
    <div className="mesh-bg flex min-h-screen min-h-[100dvh] flex-col">
      <header className="sticky top-0 z-20 border-b border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-2 px-3 sm:gap-4 sm:px-6">
          <AppBrand href="/app" className="shrink-0 text-base sm:text-lg" />

          <nav className="hidden min-w-0 flex-1 items-center justify-center gap-0.5 md:flex">
            {navItems.map((item) => {
              const active = pathname === item.href;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    "rounded-lg px-2.5 py-2 text-sm font-medium transition lg:px-3",
                    active ? "nav-link-active" : "nav-link",
                  )}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>

          <div className="ml-auto shrink-0">
            <UserMenu />
          </div>
        </div>

        <nav
          className="flex gap-1 overflow-x-auto border-t border-[var(--color-border)] px-3 py-2 md:hidden [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
          aria-label={t("nav.dashboard")}
        >
          {navItems.map((item) => {
            const active = pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "shrink-0 rounded-lg px-3 py-1.5 text-sm font-medium whitespace-nowrap transition",
                  active ? "nav-link-active" : "nav-link",
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
      </header>

      <main className="flex-1 px-3 pt-4 pb-8 sm:px-6 sm:pt-5 sm:pb-10">
        <div className="mx-auto w-full max-w-5xl">{children}</div>
      </main>
    </div>
  );
}
