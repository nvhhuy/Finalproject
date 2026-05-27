import { cn } from "@/lib/ui/cn";

type Props = React.InputHTMLAttributes<HTMLInputElement>;

export function Input({ className, ...props }: Props) {
  return (
    <input
      className={cn(
        "h-10 w-full rounded-lg border border-[var(--color-border)] bg-[var(--color-surface)] px-3 text-sm text-[var(--color-foreground)] shadow-sm transition placeholder:text-slate-400",
        "hover:border-[var(--color-border-strong)] focus:border-[var(--color-primary)] focus:outline-none focus:ring-2 focus:ring-[var(--color-primary)]/25",
        "disabled:cursor-not-allowed disabled:bg-[var(--color-surface-muted)] disabled:opacity-60",
        className,
      )}
      {...props}
    />
  );
}
