import { useEffect } from 'react'
import { X } from 'lucide-react'
import { cn } from '@/shared/lib/cn'
import { Button } from './Button'

interface ModalProps {
  open: boolean
  onClose: () => void
  title: string
  children: React.ReactNode
  className?: string
}

export function Modal({ open, onClose, title, children, className }: ModalProps) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center p-0 sm:items-center sm:p-4">
      <button
        type="button"
        className="absolute inset-0 bg-black/60"
        aria-label="Close dialog"
        onClick={onClose}
      />
      <div
        className={cn(
          'relative z-10 max-h-[92vh] w-full max-w-lg overflow-y-auto rounded-t-xl border border-[var(--color-border)] bg-[var(--color-surface-elevated)] shadow-2xl sm:rounded-xl',
          className,
        )}
        role="dialog"
        aria-modal="true"
        aria-labelledby="modal-title"
      >
        <div className="flex items-center justify-between border-b border-[var(--color-border)] px-5 py-4">
          <h2 id="modal-title" className="text-lg font-semibold text-white">
            {title}
          </h2>
          <Button variant="ghost" size="sm" onClick={onClose} aria-label="Close">
            <X className="size-4" />
          </Button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>
  )
}
