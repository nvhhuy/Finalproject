import { cn } from "@/lib/ui/cn";

type Variant = "default" | "success" | "warning" | "danger" | "primary";

const styles: Record<Variant, string> = {
  default: "bg-[var(--color-surface-muted)] text-[var(--color-foreground)]",
  primary: "bg-[var(--color-primary-soft)] text-[var(--color-accent-foreground)]",
  success: "bg-[var(--color-success-soft)] text-[var(--color-success-text)]",
  warning: "bg-[var(--color-warning-soft)] text-[var(--color-warning-text)]",
  danger: "bg-[var(--color-danger-soft)] text-[var(--color-danger-text)]",
};

export function Badge({
  children,
  variant = "default",
  className,
}: {
  children: React.ReactNode;
  variant?: Variant;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium",
        styles[variant],
        className,
      )}
    >
      {children}
    </span>
  );
}
