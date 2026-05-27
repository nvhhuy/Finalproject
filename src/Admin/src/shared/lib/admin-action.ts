import type { TFunction } from 'i18next'
import { toast, toastApiError } from '@/shared/lib/toast'

/**
 * Runs an admin mutation with standardized success/error toasts.
 * @param actionLabelKey i18n key under `userDetail.actions.*` or full path for `{{action}}` interpolation
 */
export async function runAdminAction(
  t: TFunction,
  actionLabelKey: string,
  fn: () => Promise<unknown>,
  options?: { onSuccess?: () => void },
): Promise<void> {
  const action = t(actionLabelKey)
  try {
    await fn()
    toast.success(t('toast.actionCompleted', { action }))
    options?.onSuccess?.()
  } catch (err) {
    toastApiError(err, t('toast.actionFailed', { action }))
  }
}
