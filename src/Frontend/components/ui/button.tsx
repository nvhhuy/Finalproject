import { cn } from "@/lib/ui/cn";

type Props = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md" | "lg";
};

export function Button({ className, variant = "primary", size = "md", ...props }: Props) {
  return (
    <button
      className={cn(
        "inline-flex items-center justify-center rounded-lg font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-primary)]/50 focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--color-background)] disabled:cursor-not-allowed disabled:opacity-50",
        size === "sm" && "h-8 px-3 text-sm",
        size === "md" && "h-10 px-4 text-sm",
        size === "lg" && "h-11 px-5 text-sm",
        variant === "primary" &&
          "bg-[var(--color-primary)] text-white shadow-sm hover:bg-[var(--color-primary-hover)]",
        variant === "secondary" &&
          "border border-[var(--color-border)] bg-[var(--color-surface)] text-[var(--color-foreground)] hover:border-[var(--color-border-strong)] hover:bg-[var(--color-surface-muted)]",
        variant === "ghost" && "text-[var(--color-foreground)] hover:bg-[var(--color-surface-muted)]",
        variant === "danger" && "bg-[var(--color-danger)] text-white hover:opacity-90",
        className,
      )}
      {...props}
    />
  );
}
