"use client";

import Link from "next/link";
import { useTranslation } from "react-i18next";
import { AppBrand } from "@/components/layout/AppBrand";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { Button } from "@/components/ui/button";

export function PublicHeader({ showAuthLinks = true }: { showAuthLinks?: boolean }) {
  const { t } = useTranslation();

  return (
    <header className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <AppBrand className="text-lg sm:text-xl" />
      <div className="flex min-w-0 flex-wrap items-center gap-2 lg:justify-end lg:gap-3">
        <Link href="/pricing" className="shrink-0 text-sm font-medium text-[var(--color-muted)] hover:text-[var(--color-foreground)]">
          {t("nav.pricing")}
        </Link>
        <Link href="/about" className="shrink-0 text-sm font-medium text-[var(--color-muted)] hover:text-[var(--color-foreground)]">
          {t("nav.about")}
        </Link>
        <Link href="/contact" className="shrink-0 text-sm font-medium text-[var(--color-muted)] hover:text-[var(--color-foreground)]">
          {t("nav.contact")}
        </Link>
        <ThemeSwitcher className="shrink-0" />
        <LanguageSwitcher className="shrink-0" />
        {showAuthLinks ? (
          <>
            <Link href="/login" className="shrink-0">
              <Button variant="secondary" size="sm">
                {t("common.login")}
              </Button>
            </Link>
            <Link href="/app" className="shrink-0">
              <Button size="sm">
                {t("common.openApp")}
              </Button>
            </Link>
          </>
        ) : null}
      </div>
    </header>
  );
}
