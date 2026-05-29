import { cn } from "@/lib/ui/cn";

export function Spinner({
  className,
  size = "md",
}: {
  className?: string;
  size?: "sm" | "md" | "lg";
}) {
  return (
    <span
      className={cn(
        "inline-block shrink-0 animate-spin rounded-full border-[var(--color-border)] border-t-[var(--color-primary)]",
        size === "sm" && "h-4 w-4 border-2",
        size === "md" && "h-6 w-6 border-2",
        size === "lg" && "h-9 w-9 border-[3px]",
        className,
      )}
      aria-hidden
    />
  );
}
