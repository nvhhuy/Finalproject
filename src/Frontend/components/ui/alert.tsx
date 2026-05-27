import { cn } from "@/lib/ui/cn";

type Variant = "info" | "success" | "error";

const styles: Record<Variant, string> = {
  info: "border-[var(--color-border)] bg-[var(--color-surface-muted)] text-[var(--color-foreground)]",
  success:
    "border-[var(--color-success)]/25 bg-[var(--color-success-soft)] text-[var(--color-success-text)]",
  error: "border-[var(--color-danger)]/25 bg-[var(--color-danger-soft)] text-[var(--color-danger-text)]",
};

export function Alert({
  children,
  variant = "info",
  className,
}: {
  children: React.ReactNode;
  variant?: Variant;
  className?: string;
}) {
  return (
    <div className={cn("rounded-xl border px-4 py-3 text-sm leading-relaxed", styles[variant], className)} role="alert">
      {children}
    </div>
  );
}
