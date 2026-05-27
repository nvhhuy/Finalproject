"use client";

import { useEffect } from "react";
import { detectInitialLocale, type AppLocale } from "@/i18n/locales";
import { setAppLocale } from "@/i18n/config";

/** After hydration, apply localStorage / browser preference if it differs from the server cookie snapshot. */
export function LocaleSync({ serverLocale }: { serverLocale: AppLocale }) {
  useEffect(() => {
    const preferred = detectInitialLocale();
    if (preferred !== serverLocale) {
      setAppLocale(preferred);
    }
  }, [serverLocale]);

  return null;
}
