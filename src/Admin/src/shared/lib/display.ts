import i18n from '@/i18n/config'
import { formatDate, formatDateTime } from '@/shared/lib/format'

/** Localized em dash for empty table cells. */
export function dash(): string {
  return i18n.t('common.dash')
}

export function formatEmpty(
  value: string | null | undefined,
  fallback?: string,
): string {
  if (value == null || value === '') {
    return fallback ?? dash()
  }
  return value
}

export function formatDateOrDash(
  value: string | null | undefined,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (!value) return dash()
  return formatDate(value, options)
}

export function formatDateTimeOrDash(
  value: string | null | undefined,
  options?: Intl.DateTimeFormatOptions,
): string {
  if (!value) return dash()
  return formatDateTime(value, options)
}
