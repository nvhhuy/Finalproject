"use client";

import { useTranslation } from "react-i18next";
import { AppBrand } from "@/components/layout/AppBrand";
import { LanguageSwitcher } from "@/components/layout/LanguageSwitcher";
import { ThemeSwitcher } from "@/components/layout/ThemeSwitcher";
import { APP_NAME } from "@/lib/config/branding";

export function AuthLayout({
  children,
  title,
  subtitle,
}: {
  children: React.ReactNode;
  title: string;
  subtitle?: string;
}) {
  const { t } = useTranslation();

  return (
    <div className="mesh-bg flex min-h-screen min-h-[100dvh]">
      <aside className="relative hidden w-[40%] max-w-xl overflow-hidden border-r border-[var(--color-border)] bg-[var(--color-surface)] lg:flex lg:flex-col xl:w-[44%]">
        <div className="absolute inset-0 bg-gradient-to-br from-indigo-600 via-indigo-700 to-violet-800" />
        <div className="absolute inset-0 bg-[url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iNjAiIGhlaWdodD0iNjAiIHZpZXdCb3g9IjAgMCA2MCA2MCIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIj48ZyBmaWxsPSJub25lIiBmaWxsLXJ1bGU9ImV2ZW5vZGQiPjxwYXRoIGQ9Ik0zNiAxOGMtOS45NDEgMC0xOCA4LjA1OS0xOCAxOHM4LjA1OSAxOCAxOCAxOCAxOC04LjA1OSAxOC0xOC04LjA1OS0xOC0xOC0xOHoiIHN0cm9rZT0icmdiYSgyNTUsMjU1LDI1NSwwLjA4KSIvPjwvZz48L2c+PC9zdmc+')] opacity-60" />
        <div className="relative flex flex-1 flex-col justify-between p-8 text-white xl:p-10">
          <AppBrand className="text-lg text-white" />
          <div>
            <p className="text-sm font-medium text-indigo-200">{t("landing.tag")}</p>
            <h2 className="mt-2 max-w-sm text-2xl font-semibold leading-tight tracking-tight xl:text-3xl">
              {t("landing.title")}
            </h2>
            <p className="mt-4 max-w-md text-sm leading-relaxed text-indigo-100/90">
              {t("authLayout.tagline")}
            </p>
          </div>
          <p className="text-xs text-indigo-200/80">
            © {new Date().getFullYear()} {APP_NAME}
          </p>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between gap-3 px-4 py-3 sm:px-6 sm:py-4">
          <AppBrand className="text-sm lg:hidden" />
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2 sm:gap-3">
            <ThemeSwitcher />
            <LanguageSwitcher />
          </div>
        </header>

        <main className="flex flex-1 items-center justify-center px-4 pb-10 sm:px-6 sm:pb-12">
          <div className="w-full max-w-md">
            <div className="mb-5 sm:mb-6">
              <h1 className="text-xl font-semibold tracking-tight text-[var(--color-foreground)] sm:text-2xl">
                {title}
              </h1>
              {subtitle ? (
                <p className="mt-2 text-sm leading-relaxed text-[var(--color-muted)]">{subtitle}</p>
              ) : null}
            </div>
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
