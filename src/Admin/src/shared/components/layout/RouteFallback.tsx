import { useTranslation } from 'react-i18next'

/** Shown while a lazy-loaded route chunk is downloading. */
export function RouteFallback() {
  const { t } = useTranslation()

  return (
    <div
      className="flex flex-1 flex-col items-center justify-center gap-3 p-8"
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <div className="size-8 animate-spin rounded-full border-2 border-slate-600 border-t-[var(--color-accent)]" />
      <p className="text-sm text-slate-400">{t('common.loading')}</p>
    </div>
  )
}
