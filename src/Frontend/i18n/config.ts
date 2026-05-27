import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import en from "@/i18n/locales/en.json";
import vi from "@/i18n/locales/vi.json";
import zh from "@/i18n/locales/zh.json";
import {
  localeToHtmlLang,
  LOCALE_STORAGE_KEY,
  setLocaleCookie,
  type AppLocale,
} from "@/i18n/locales";

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    vi: { translation: vi },
    zh: { translation: zh },
  },
  lng: "en",
  fallbackLng: "en",
  interpolation: { escapeValue: false },
});

/** Align i18n (and <html lang>) with the server locale before render — avoids hydration mismatch. */
export function ensureI18nLocale(locale: AppLocale) {
  if (i18n.language !== locale) {
    i18n.changeLanguage(locale);
  }
  if (typeof document !== "undefined") {
    document.documentElement.lang = localeToHtmlLang(locale);
  }
}

export function setAppLocale(locale: AppLocale) {
  if (typeof window !== "undefined") {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
    setLocaleCookie(locale);
  }
  void i18n.changeLanguage(locale);
  if (typeof document !== "undefined") {
    document.documentElement.lang = localeToHtmlLang(locale);
  }
}

export default i18n;
