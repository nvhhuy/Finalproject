export const LOCALE_STORAGE_KEY = 'admin-locale'

export const SUPPORTED_LOCALES = ['en', 'vi', 'zh'] as const
export type AppLocale = (typeof SUPPORTED_LOCALES)[number]

export const LOCALE_LABELS: Record<AppLocale, string> = {
  en: 'English',
  vi: 'Tiếng Việt',
  zh: '中文',
}

export function isAppLocale(value: string): value is AppLocale {
  return (SUPPORTED_LOCALES as readonly string[]).includes(value)
}

export function detectInitialLocale(): AppLocale {
  const stored = localStorage.getItem(LOCALE_STORAGE_KEY)
  if (stored && isAppLocale(stored)) {
    return stored
  }
  const browser = navigator.language.toLowerCase()
  if (browser.startsWith('vi')) return 'vi'
  if (browser.startsWith('zh')) return 'zh'
  return 'en'
}

export function localeToBcp47(locale: AppLocale): string {
  if (locale === 'zh') return 'zh-CN'
  if (locale === 'vi') return 'vi-VN'
  return 'en-US'
}
