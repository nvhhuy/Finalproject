import { cn } from "@/lib/ui/cn";
import type { LabelHTMLAttributes } from "react";

export function Label({ className, ...props }: LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label className={cn("text-sm font-medium text-[var(--color-foreground)]", className)} {...props} />
  );
}
