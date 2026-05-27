import i18n from '@/i18n/config'
import { isAppLocale, localeToBcp47 } from '@/i18n/locales'

function currentBcp47() {
  const lang = i18n.language
  return isAppLocale(lang) ? localeToBcp47(lang) : 'en-US'
}

export function formatCount(value: number, options?: Intl.NumberFormatOptions) {
  return value.toLocaleString(currentBcp47(), options)
}
