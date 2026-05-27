import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertCircle, CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { cn } from '@/shared/lib/cn'
import { useToastStore, type Toast, type ToastVariant } from '@/shared/stores/toast-store'

const variantStyles: Record<ToastVariant, string> = {
  success: 'border-emerald-500/40 bg-emerald-950/90 text-emerald-100',
  error: 'border-red-500/40 bg-red-950/90 text-red-100',
  warning: 'border-amber-500/40 bg-amber-950/90 text-amber-100',
  info: 'border-blue-500/40 bg-slate-900/95 text-slate-100',
}

const icons: Record<ToastVariant, typeof CheckCircle2> = {
  success: CheckCircle2,
  error: XCircle,
  warning: AlertCircle,
  info: Info,
}

function ToastItem({ toast }: { toast: Toast }) {
  const { t } = useTranslation()
  const dismiss = useToastStore((s) => s.dismiss)
  const Icon = icons[toast.variant]

  useEffect(() => {
    const timer = window.setTimeout(() => dismiss(toast.id), toast.durationMs)
    return () => window.clearTimeout(timer)
  }, [dismiss, toast.durationMs, toast.id])

  return (
    <div
      role="status"
      className={cn(
        'pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border px-4 py-3 shadow-lg backdrop-blur-sm',
        variantStyles[toast.variant],
      )}
    >
      <Icon className="mt-0.5 size-5 shrink-0 opacity-90" aria-hidden />
      <p className="flex-1 text-sm leading-snug">{toast.message}</p>
      <button
        type="button"
        onClick={() => dismiss(toast.id)}
        className="shrink-0 rounded p-0.5 opacity-70 transition-opacity hover:opacity-100"
        aria-label={t('common.dismissNotification')}
      >
        <X className="size-4" />
      </button>
    </div>
  )
}

export function Toaster() {
  const toasts = useToastStore((s) => s.toasts)

  return (
    <div
      aria-live="polite"
      aria-relevant="additions"
      className="pointer-events-none fixed inset-x-0 bottom-0 z-[100] flex flex-col items-center gap-2 p-4 sm:items-end sm:p-6"
    >
      {toasts.map((t) => (
        <ToastItem key={t.id} toast={t} />
      ))}
    </div>
  )
}
