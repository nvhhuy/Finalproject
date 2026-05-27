import i18n from "@/i18n/config";
import { isAppLocale, localeToBcp47 } from "@/i18n/locales";

function currentBcp47() {
  const lang = i18n.language;
  return isAppLocale(lang) ? localeToBcp47(lang) : "en-US";
}

export function formatCount(value: number, options?: Intl.NumberFormatOptions) {
  return value.toLocaleString(currentBcp47(), options);
}

export function formatBytes(bytes: number) {
  if (bytes < 1024) return `${formatCount(bytes)} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}
