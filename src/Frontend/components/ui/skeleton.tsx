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

export function CardSkeleton({ lines = 3 }: { lines?: number }) {
  return (
    <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5 shadow-sm">
      <Skeleton className="mb-4 h-5 w-40" />
      <div className="space-y-2">
        {Array.from({ length: lines }).map((_, i) => (
          <Skeleton key={i} className={cn("h-4", i === lines - 1 ? "w-2/3" : "w-full")} />
        ))}
      </div>
    </div>
  );
}

export function ProfileSkeleton() {
  return (
    <div className="space-y-6">
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5">
        <Skeleton className="mb-2 h-5 w-32" />
        <Skeleton className="mb-6 h-4 w-56" />
        <div className="mb-4 flex gap-2">
          <Skeleton className="h-6 w-24 rounded-full" />
          <Skeleton className="h-6 w-20 rounded-full" />
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
        <div className="mt-4 flex gap-2">
          <Skeleton className="h-10 w-24" />
          <Skeleton className="h-10 w-36" />
        </div>
      </div>
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5">
        <Skeleton className="mb-2 h-5 w-28" />
        <Skeleton className="mb-4 h-4 w-full max-w-md" />
        <Skeleton className="h-14 w-full max-w-xs rounded-lg" />
      </div>
    </div>
  );
}

export function WalletSkeleton() {
  return (
    <div className="space-y-4">
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5">
        <Skeleton className="mb-4 h-5 w-28" />
        <Skeleton className="mb-2 h-10 w-32" />
        <Skeleton className="mb-4 h-4 w-48" />
        <div className="flex gap-2">
          <Skeleton className="h-10 w-[140px]" />
          <Skeleton className="h-10 w-24" />
        </div>
      </div>
      <CardSkeleton lines={4} />
      <CardSkeleton lines={3} />
      <CardSkeleton lines={5} />
    </div>
  );
}

export function TenantSkeleton() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-10 w-full max-w-md" />
      <CardSkeleton lines={4} />
      <CardSkeleton lines={5} />
      <CardSkeleton lines={4} />
    </div>
  );
}

export function SupportSkeleton() {
  return (
    <div className="grid gap-4 lg:grid-cols-[1.1fr_1.4fr]">
      <CardSkeleton lines={5} />
      <div className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5">
        <Skeleton className="mb-4 h-5 w-32" />
        <div className="mb-3 flex gap-2">
          <Skeleton className="h-10 flex-1" />
          <Skeleton className="h-10 w-28" />
          <Skeleton className="h-10 w-20" />
        </div>
        <div className="grid gap-2 lg:grid-cols-2">
          <Skeleton className="h-80 w-full rounded-lg" />
          <Skeleton className="h-80 w-full rounded-lg" />
        </div>
      </div>
    </div>
  );
}

export function PricingCardsSkeleton() {
  return (
    <div className="mt-10 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 3 }).map((_, i) => (
        <div
          key={i}
          className="flex flex-col rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5 shadow-sm"
        >
          <Skeleton className="mb-3 h-6 w-28" />
          <Skeleton className="mb-1 h-8 w-24" />
          <Skeleton className="mb-4 h-4 w-16" />
          <Skeleton className="mb-2 h-4 w-full" />
          <Skeleton className="mb-2 h-4 w-4/5" />
          <Skeleton className="mt-auto h-10 w-full" />
        </div>
      ))}
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
