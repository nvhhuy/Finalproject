import { cn } from '@/shared/lib/cn'
import type { ButtonHTMLAttributes } from 'react'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'sm' | 'md'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: Size
  loading?: boolean
}

const variants: Record<Variant, string> = {
  primary:
    'bg-[var(--color-accent)] hover:bg-[var(--color-accent-hover)] text-white border-transparent',
  secondary:
    'bg-[var(--color-surface-muted)] hover:bg-[var(--color-border)] text-slate-100 border-[var(--color-border)]',
  danger: 'bg-red-600/90 hover:bg-red-600 text-white border-transparent',
  ghost: 'bg-transparent hover:bg-[var(--color-surface-muted)] text-slate-200 border-transparent',
}

const sizes: Record<Size, string> = {
  sm: 'px-2.5 py-1 text-xs',
  md: 'px-4 py-2 text-sm',
}

export function Button({
  className,
  variant = 'primary',
  size = 'md',
  loading,
  disabled,
  children,
  ...props
}: ButtonProps) {
  return (
    <button
      type="button"
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg border font-medium transition-colors',
        'focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--color-accent)]',
        'disabled:pointer-events-none disabled:opacity-50',
        variants[variant],
        sizes[size],
        className,
      )}
      disabled={disabled || loading}
      {...props}
    >
      {loading ? (
        <span className="size-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />
      ) : null}
      {children}
    </button>
  )
}
