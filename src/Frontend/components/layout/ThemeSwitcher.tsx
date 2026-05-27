"use client";

import { useTranslation } from "react-i18next";
import { useTheme } from "@/lib/theme/ThemeProvider";
import { THEME_PREFERENCES, type ThemePreference } from "@/lib/theme/theme";
import { cn } from "@/lib/ui/cn";

const icons: Record<ThemePreference, string> = {
  light: "☀",
  dark: "☾",
  auto: "◐",
};

export function ThemeSwitcher({
  className,
  variant = "toolbar",
}: {
  className?: string;
  variant?: "toolbar" | "menu";
}) {
  const { t } = useTranslation();
  const { preference, setPreference } = useTheme();

  if (variant === "menu") {
    return (
      <div
        className={cn(
          "grid grid-cols-3 gap-1 rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-muted)] p-1",
          className,
        )}
        role="group"
        aria-label={t("theme.label")}
      >
        {THEME_PREFERENCES.map((mode) => (
          <button
            key={mode}
            type="button"
            onClick={() => setPreference(mode)}
            title={t(`theme.${mode}`)}
            className={cn(
              "flex flex-col items-center justify-center gap-0.5 rounded-md px-1 py-2 text-center transition",
              preference === mode
                ? "theme-option-active shadow-sm"
                : "text-[var(--color-muted)] hover:bg-[var(--color-surface)] hover:text-[var(--color-foreground)]",
            )}
            aria-pressed={preference === mode}
            aria-label={t(`theme.${mode}`)}
          >
            <span className="text-base leading-none" aria-hidden>
              {icons[mode]}
            </span>
            <span className="text-[10px] font-medium leading-tight sm:text-xs">{t(`theme.${mode}`)}</span>
          </button>
        ))}
      </div>
    );
  }

  return (
    <div
      className={cn(
        "inline-flex rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] p-0.5 shadow-sm",
        className,
      )}
      role="group"
      aria-label={t("theme.label")}
    >
      {THEME_PREFERENCES.map((mode) => (
        <button
          key={mode}
          type="button"
          onClick={() => setPreference(mode)}
          title={t(`theme.${mode}`)}
          className={cn(
            "flex h-8 min-w-[2.25rem] items-center justify-center gap-1 rounded-md px-2 text-sm transition",
            preference === mode
              ? "theme-option-active"
              : "text-[var(--color-muted)] hover:bg-[var(--color-surface-muted)] hover:text-[var(--color-foreground)]",
          )}
          aria-pressed={preference === mode}
          aria-label={t(`theme.${mode}`)}
        >
          <span aria-hidden>{icons[mode]}</span>
          <span className="hidden md:inline">{t(`theme.${mode}`)}</span>
        </button>
      ))}
    </div>
  );
}
