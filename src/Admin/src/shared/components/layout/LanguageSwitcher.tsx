import { useTranslation } from 'react-i18next'
import { Languages } from 'lucide-react'
import { setAppLocale } from '@/i18n/config'
import {
  LOCALE_LABELS,
  SUPPORTED_LOCALES,
  type AppLocale,
} from '@/i18n/locales'
import { formSelectClass, formSelectCompactClass } from '@/shared/constants/styles'
import { cn } from '@/shared/lib/cn'

export function LanguageSwitcher({
  className,
  variant = 'default',
}: {
  className?: string
  variant?: 'default' | 'compact'
}) {
  const { i18n, t } = useTranslation()

  if (variant === 'compact') {
    return (
      <div className={cn('flex items-center gap-2', className)}>
        <Languages className="size-4 shrink-0 text-slate-400" aria-hidden />
        <select
          id="admin-locale-compact"
          value={i18n.language}
          onChange={(e) => setAppLocale(e.target.value as AppLocale)}
          aria-label={t('common.language')}
          className={cn(
            formSelectCompactClass,
            'min-w-[7.5rem] py-1.5 pl-2 pr-7 text-xs sm:min-w-[8.5rem] sm:text-sm',
          )}
        >
          {SUPPORTED_LOCALES.map((locale) => (
            <option key={locale} value={locale}>
              {LOCALE_LABELS[locale]}
            </option>
          ))}
        </select>
      </div>
    )
  }

  return (
    <div className={cn('space-y-1.5', className)}>
      <label
        htmlFor="admin-locale"
        className="flex items-center gap-1.5 text-xs font-medium text-slate-500"
      >
        <Languages className="size-3.5" aria-hidden />
        {t('common.language')}
      </label>
      <select
        id="admin-locale"
        value={i18n.language}
        onChange={(e) => setAppLocale(e.target.value as AppLocale)}
        className={cn(formSelectClass, 'w-full')}
      >
        {SUPPORTED_LOCALES.map((locale) => (
          <option key={locale} value={locale}>
            {LOCALE_LABELS[locale]}
          </option>
        ))}
      </select>
    </div>
  )
}
