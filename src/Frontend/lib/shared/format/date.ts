import i18n from "@/i18n/config";
import { isAppLocale, localeToBcp47 } from "@/i18n/locales";

function currentBcp47() {
  const lang = i18n.language;
  return isAppLocale(lang) ? localeToBcp47(lang) : "en-US";
}

function toDate(value: string | Date): Date {
  return typeof value === "string" ? new Date(value) : value;
}

/** Locale-aware date (uses active i18n language). */
export function formatDate(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  return toDate(value).toLocaleDateString(currentBcp47(), options);
}

/** Locale-aware date + time. */
export function formatDateTime(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  return toDate(value).toLocaleString(currentBcp47(), options);
}

/** Relative time, e.g. "2 hours ago" (needs modern runtime). */
export function formatRelativeTime(value: string | Date, base: Date = new Date()) {
  const date = toDate(value);
  const diffSec = Math.round((date.getTime() - base.getTime()) / 1000);
  const abs = Math.abs(diffSec);
  const rtf = new Intl.RelativeTimeFormat(currentBcp47(), { numeric: "auto" });

  if (abs < 60) return rtf.format(diffSec, "second");
  if (abs < 3600) return rtf.format(Math.round(diffSec / 60), "minute");
  if (abs < 86400) return rtf.format(Math.round(diffSec / 3600), "hour");
  return rtf.format(Math.round(diffSec / 86400), "day");
}
