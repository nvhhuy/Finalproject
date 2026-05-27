export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string
  description?: string
  actions?: React.ReactNode
}) {
  return (
    <header className="flex flex-col gap-4 border-b border-[var(--color-border)] bg-[var(--color-surface-elevated)]/50 px-4 py-4 sm:flex-row sm:flex-wrap sm:items-start sm:justify-between sm:px-6 sm:py-5 lg:px-8 lg:py-6">
      <div className="min-w-0">
        <h1 className="text-xl font-semibold tracking-tight text-white sm:text-2xl">{title}</h1>
        {description ? (
          <p className="mt-1 text-sm text-slate-400">{description}</p>
        ) : null}
      </div>
      {actions ? (
        <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>
      ) : null}
    </header>
  )
}
