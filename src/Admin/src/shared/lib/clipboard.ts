import i18n from '@/i18n/config'
import { toast } from '@/shared/lib/toast'

export async function copyWithToast(text: string, label: string): Promise<void> {
  await navigator.clipboard.writeText(text)
  toast.success(i18n.t('toast.copiedToClipboard', { label }))
}
