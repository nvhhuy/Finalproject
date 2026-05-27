import i18n from '@/i18n/config'
import { useToastStore, type ToastVariant } from '@/shared/stores/toast-store'
import { ApiClientError } from '@/shared/lib/errors'

const DEFAULT_DURATION_MS = 5_000

function push(message: string, variant: ToastVariant, durationMs = DEFAULT_DURATION_MS) {
  return useToastStore.getState().push({ message, variant, durationMs })
}

export const toast = {
  success: (message: string, durationMs?: number) => push(message, 'success', durationMs),
  error: (message: string, durationMs?: number) => push(message, 'error', durationMs ?? 7_000),
  info: (message: string, durationMs?: number) => push(message, 'info', durationMs),
  warning: (message: string, durationMs?: number) => push(message, 'warning', durationMs),
  dismiss: (id: string) => useToastStore.getState().dismiss(id),
}

export function getApiErrorMessage(err: unknown, fallbackKey = 'errors.requestFailed'): string {
  if (err instanceof ApiClientError) {
    if (err.code === 'ConcurrencyConflict') {
      return i18n.t('errors.concurrencyConflict')
    }
    return err.message
  }
  if (err instanceof Error) {
    return err.message
  }
  return i18n.t(fallbackKey)
}

export function toastApiError(err: unknown, fallbackKey = 'errors.requestFailed') {
  toast.error(getApiErrorMessage(err, fallbackKey))
}
