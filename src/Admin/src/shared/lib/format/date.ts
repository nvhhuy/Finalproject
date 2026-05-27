import i18n from '@/i18n/config'
import { isAppLocale, localeToBcp47 } from '@/i18n/locales'

function currentBcp47() {
  const lang = i18n.language
  return isAppLocale(lang) ? localeToBcp47(lang) : 'en-US'
}

function toDate(value: string | Date): Date {
  return typeof value === 'string' ? new Date(value) : value
}

export function formatDate(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  return toDate(value).toLocaleDateString(currentBcp47(), options)
}

export function formatDateTime(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  return toDate(value).toLocaleString(currentBcp47(), options)
}
