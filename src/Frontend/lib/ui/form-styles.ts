import { cn } from "@/lib/ui/cn";

/** Shared native control styling for selects and textareas in app pages. */
export const formSelectClass = cn(
  "h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm",
  "text-[var(--color-foreground)] transition",
  "focus:border-[var(--color-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/25",
);

export const formTextareaClass = cn(
  "min-h-24 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm",
  "text-[var(--color-foreground)] placeholder:text-[var(--color-muted)] transition",
  "focus:border-[var(--color-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/25",
);
