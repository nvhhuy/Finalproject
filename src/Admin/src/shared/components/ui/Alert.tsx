import { cn } from '@/shared/lib/cn'

type Variant = 'info' | 'success' | 'error' | 'warning'

const styles: Record<Variant, string> = {
  info: 'border-blue-500/40 bg-blue-950/40 text-blue-200',
  success: 'border-emerald-500/40 bg-emerald-950/40 text-emerald-200',
  error: 'border-red-500/40 bg-red-950/40 text-red-200',
  warning: 'border-amber-500/40 bg-amber-950/40 text-amber-200',
}

export function Alert({
  variant = 'info',
  children,
  className,
}: {
  variant?: Variant
  children: React.ReactNode
  className?: string
}) {
  return (
    <div
      className={cn('rounded-lg border px-4 py-3 text-sm', styles[variant], className)}
      role="alert"
    >
      {children}
    </div>
  )
}
