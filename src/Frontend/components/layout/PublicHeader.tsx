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
    <header className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <AppBrand className="text-lg sm:text-xl" />
      <div className="flex flex-wrap items-center gap-2 sm:justify-end sm:gap-3">
        <ThemeSwitcher />
        <LanguageSwitcher />
        {showAuthLinks ? (
          <>
            <Link href="/login" className="w-full sm:w-auto">
              <Button variant="secondary" size="sm" className="w-full sm:w-auto">
                {t("common.login")}
              </Button>
            </Link>
            <Link href="/app" className="w-full sm:w-auto">
              <Button size="sm" className="w-full sm:w-auto">
                {t("common.openApp")}
              </Button>
            </Link>
          </>
        ) : null}
      </div>
    </header>
  );
}
