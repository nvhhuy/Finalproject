import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/components/ui/Button'
import { cn } from '@/shared/lib/cn'

interface DataTablePaginationProps {
  page: number
  totalPages: number
  onPageChange: (page: number) => void
  className?: string
}

export function DataTablePagination({
  page,
  totalPages,
  onPageChange,
  className,
}: DataTablePaginationProps) {
  const { t } = useTranslation()

  if (totalPages <= 1) return null

  return (
    <div
      className={cn(
        'flex flex-col gap-3 border-t border-[var(--color-border)] px-4 py-3 sm:flex-row sm:items-center sm:justify-between',
        className,
      )}
    >
      <Button
        size="sm"
        variant="secondary"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        {t('common.previous')}
      </Button>
      <span className="text-sm text-slate-400">
        {t('common.pageOf', { page, total: totalPages })}
      </span>
      <Button
        size="sm"
        variant="secondary"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        {t('common.next')}
      </Button>
    </div>
  )
}
