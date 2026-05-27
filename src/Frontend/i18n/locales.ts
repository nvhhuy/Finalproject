export const LOCALE_STORAGE_KEY = "frontend-locale";

export const SUPPORTED_LOCALES = ["en", "vi", "zh"] as const;
export type AppLocale = (typeof SUPPORTED_LOCALES)[number];

export const LOCALE_LABELS: Record<AppLocale, string> = {
  en: "English",
  vi: "Tiếng Việt",
  zh: "中文",
};

export function isAppLocale(value: string): value is AppLocale {
  return (SUPPORTED_LOCALES as readonly string[]).includes(value);
}

export function localeToHtmlLang(locale: AppLocale): string {
  return locale === "zh" ? "zh-Hans" : locale;
}

/** BCP 47 tag for `Intl` formatters (dates, numbers). */
export function localeToBcp47(locale: AppLocale): string {
  if (locale === "zh") return "zh-CN";
  if (locale === "vi") return "vi-VN";
  return "en-US";
}

/** Read locale from request cookies (Server Components / layout). */
export function localeFromCookieValue(value: string | undefined): AppLocale {
  if (value && isAppLocale(value)) return value;
  return "en";
}

export function setLocaleCookie(locale: AppLocale) {
  if (typeof document === "undefined") return;
  document.cookie = `${LOCALE_STORAGE_KEY}=${locale};path=/;max-age=31536000;SameSite=Lax`;
}

export function detectInitialLocale(): AppLocale {
  if (typeof window === "undefined") return "en";
  const stored = localStorage.getItem(LOCALE_STORAGE_KEY);
  if (stored && isAppLocale(stored)) return stored;
  const match = document.cookie.match(new RegExp(`(?:^|; )${LOCALE_STORAGE_KEY}=([^;]*)`));
  if (match?.[1] && isAppLocale(match[1])) return match[1];
  const browser = navigator.language.toLowerCase();
  if (browser.startsWith("vi")) return "vi";
  if (browser.startsWith("zh")) return "zh";
  return "en";
}
