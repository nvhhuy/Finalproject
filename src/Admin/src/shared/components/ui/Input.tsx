import { cn } from '@/shared/lib/cn'
import type { InputHTMLAttributes } from 'react'

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
}

export function Input({ className, label, error, id, ...props }: InputProps) {
  const inputId = id ?? label?.toLowerCase().replace(/\s+/g, '-')
  return (
    <div className="space-y-1.5">
      {label ? (
        <label htmlFor={inputId} className="block text-sm font-medium text-slate-300">
          {label}
        </label>
      ) : null}
      <input
        id={inputId}
        className={cn(
          'w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface-elevated)] px-3 py-2 text-sm text-slate-100',
          'placeholder:text-slate-500 focus:border-[var(--color-accent)] focus:outline-none focus:ring-1 focus:ring-[var(--color-accent)]',
          error && 'border-red-500 focus:border-red-500 focus:ring-red-500',
          className,
        )}
        {...props}
      />
      {error ? <p className="text-xs text-red-400">{error}</p> : null}
    </div>
  )
}
