import { cn } from '@/shared/lib/cn'

export function PageContent({
  children,
  className,
}: {
  children: React.ReactNode
  className?: string
}) {
  return <div className={cn('space-y-4 p-4 sm:space-y-6 sm:p-6 lg:p-8', className)}>{children}</div>
}
