import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from '@/i18n/locales/en.json'
import vi from '@/i18n/locales/vi.json'
import zh from '@/i18n/locales/zh.json'
import { detectInitialLocale, LOCALE_STORAGE_KEY, type AppLocale } from '@/i18n/locales'

const initialLocale = detectInitialLocale()

void i18n.use(initReactI18next).init({
  resources: {
    en: { translation: en },
    vi: { translation: vi },
    zh: { translation: zh },
  },
  lng: initialLocale,
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

document.documentElement.lang =
  initialLocale === 'zh' ? 'zh-Hans' : initialLocale

export function setAppLocale(locale: AppLocale) {
  localStorage.setItem(LOCALE_STORAGE_KEY, locale)
  void i18n.changeLanguage(locale)
  document.documentElement.lang = locale === 'zh' ? 'zh-Hans' : locale
}

export default i18n
