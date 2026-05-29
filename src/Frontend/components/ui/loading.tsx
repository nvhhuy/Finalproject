"use client";

import { useTranslation } from "react-i18next";
import { cn } from "@/lib/ui/cn";
import { Spinner } from "@/components/ui/spinner";

export { Spinner };

export function LoadingState({
  className,
  label,
  compact,
}: {
  className?: string;
  label?: string;
  compact?: boolean;
}) {
  const { t } = useTranslation();
  const text = label ?? t("common.loading");

  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center gap-3 text-center",
        compact ? "py-6" : "py-12",
        className,
      )}
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <Spinner size={compact ? "md" : "lg"} />
      <p className="text-sm text-[var(--color-muted)]">{text}</p>
    </div>
  );
}

export function InlineLoading({ className, label }: { className?: string; label?: string }) {
  const { t } = useTranslation();
  return (
    <span
      className={cn("inline-flex items-center gap-2 text-sm text-[var(--color-muted)]", className)}
      role="status"
      aria-live="polite"
      aria-busy="true"
    >
      <Spinner size="sm" />
      <span>{label ?? t("common.loading")}</span>
    </span>
  );
}
