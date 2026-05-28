import { cn } from "@/lib/ui/cn";

export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn("animate-pulse rounded-lg bg-[var(--color-surface-muted)]", className)}
      aria-hidden
    />
  );
}

export function PageSkeleton() {
  return (
    <div className="space-y-5">
      <Skeleton className="h-8 w-48" />
      <Skeleton className="h-4 w-full max-w-md" />
      <Skeleton className="h-40 w-full rounded-2xl" />
      <Skeleton className="h-32 w-full rounded-2xl" />
    </div>
  );
}

export function AppShellSkeleton() {
  return (
    <div className="mesh-bg flex min-h-screen min-h-[100dvh] flex-col">
      <header className="border-b border-[var(--color-border)] bg-[var(--color-surface)]">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4 sm:px-6">
          <Skeleton className="h-6 w-28" />
          <div className="hidden flex-1 justify-center gap-2 md:flex">
            <Skeleton className="h-8 w-20" />
            <Skeleton className="h-8 w-20" />
            <Skeleton className="h-8 w-16" />
          </div>
          <Skeleton className="ml-auto h-10 w-24 rounded-full" />
        </div>
      </header>
      <main className="flex-1 px-4 pt-4 pb-10 sm:px-6">
        <div className="mx-auto max-w-5xl">
          <PageSkeleton />
        </div>
      </main>
    </div>
  );
}

export function LandingSkeleton() {
  return (
    <div className="mesh-bg min-h-screen min-h-[100dvh] px-4 py-8 sm:px-6">
      <div className="mx-auto max-w-6xl">
        <div className="flex justify-between gap-4">
          <Skeleton className="h-7 w-32" />
          <Skeleton className="h-9 w-40" />
        </div>
        <div className="mt-12 grid gap-8 lg:grid-cols-2">
          <div className="space-y-4">
            <Skeleton className="h-6 w-24 rounded-full" />
            <Skeleton className="h-12 w-full max-w-lg" />
            <Skeleton className="h-20 w-full max-w-xl" />
            <div className="flex gap-3">
              <Skeleton className="h-11 w-36" />
              <Skeleton className="h-11 w-28" />
            </div>
          </div>
          <Skeleton className="h-80 w-full rounded-2xl" />
        </div>
      </div>
    </div>
  );
}
