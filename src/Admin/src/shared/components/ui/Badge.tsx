import { cn } from '@/shared/lib/cn'

type Tone = 'default' | 'info' | 'success' | 'warning' | 'danger'

const tones: Record<Tone, string> = {
  default: 'bg-slate-700 text-slate-200',
  info: 'bg-sky-900/50 text-sky-300',
  success: 'bg-emerald-900/60 text-emerald-300',
  warning: 'bg-amber-900/50 text-amber-300',
  danger: 'bg-red-900/50 text-red-300',
}

export function Badge({
  children,
  tone = 'default',
  className,
}: {
  children: React.ReactNode
  tone?: Tone
  className?: string
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
        tones[tone],
        className,
      )}
    >
      {children}
    </span>
  )
}
