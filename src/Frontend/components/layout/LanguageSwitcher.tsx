"use client";

import { useTranslation } from "react-i18next";
import { setAppLocale } from "@/i18n/config";
import { LOCALE_LABELS, SUPPORTED_LOCALES, type AppLocale } from "@/i18n/locales";
import { cn } from "@/lib/ui/cn";

function SelectChevron() {
  return (
    <svg
      viewBox="0 0 20 20"
      fill="currentColor"
      aria-hidden
      className="pointer-events-none absolute top-1/2 right-2.5 h-4 w-4 -translate-y-1/2 text-[var(--color-muted)]"
    >
      <path
        fillRule="evenodd"
        d="M5.23 7.21a.75.75 0 011.06.02L10 10.94l3.71-3.71a.75.75 0 111.06 1.06l-4.24 4.25a.75.75 0 01-1.06 0L5.21 8.29a.75.75 0 01.02-1.08z"
        clipRule="evenodd"
      />
    </svg>
  );
}

export function LanguageSwitcher({
  className,
  variant = "default",
}: {
  className?: string;
  variant?: "default" | "menu";
}) {
  const { i18n, t } = useTranslation();

  const selectClass = cn(
    "w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] text-sm text-[var(--color-foreground)] shadow-sm",
    "focus:border-[var(--color-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/25",
    variant === "menu" ? "h-9 appearance-none py-2 pr-9 pl-3" : "h-9 px-2.5",
  );

  if (variant === "menu") {
    return (
      <label className={cn("relative block text-sm", className)}>
        <span className="sr-only">{t("common.language")}</span>
        <select
          value={i18n.language}
          onChange={(e) => setAppLocale(e.target.value as AppLocale)}
          className={selectClass}
          aria-label={t("common.language")}
        >
          {SUPPORTED_LOCALES.map((locale) => (
            <option key={locale} value={locale}>
              {LOCALE_LABELS[locale]}
            </option>
          ))}
        </select>
        <SelectChevron />
      </label>
    );
  }

  return (
    <label className={cn("flex items-center gap-2 text-sm", className)}>
      <span className="sr-only">{t("common.language")}</span>
      <select
        value={i18n.language}
        onChange={(e) => setAppLocale(e.target.value as AppLocale)}
        className={selectClass}
        aria-label={t("common.language")}
      >
        {SUPPORTED_LOCALES.map((locale) => (
          <option key={locale} value={locale}>
            {LOCALE_LABELS[locale]}
          </option>
        ))}
      </select>
    </label>
  );
}
