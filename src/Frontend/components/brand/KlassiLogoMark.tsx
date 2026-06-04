import { cn } from "@/lib/ui/cn";

export type KlassiLogoVariant = "brand" | "inverse" | "mono";

type Props = {
  size?: number;
  variant?: KlassiLogoVariant;
  className?: string;
  "aria-hidden"?: boolean;
};

/**
 * Klassi logomark — rounded tile + geometric K (emerald v4.1).
 * brand: green tile / white K (headers, light UI)
 * inverse: white tile / green K (green gradient sidebar)
 * mono: transparent / primary K (minimal)
 */
export function KlassiLogoMark({
  size = 28,
  variant = "brand",
  className,
  "aria-hidden": ariaHidden = true,
}: Props) {
  const tile =
    variant === "brand"
      ? "var(--color-primary)"
      : variant === "inverse"
        ? "#ffffff"
        : "transparent";
  const letter =
    variant === "brand"
      ? "#ffffff"
      : variant === "inverse"
        ? "var(--color-primary-hover)"
        : "var(--color-primary)";
  const stroke = variant === "mono" ? "var(--color-primary)" : undefined;

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 32 32"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={cn("shrink-0", className)}
      aria-hidden={ariaHidden}
    >
      <rect
        x={variant === "mono" ? 1 : 0}
        y={variant === "mono" ? 1 : 0}
        width={variant === "mono" ? 30 : 32}
        height={variant === "mono" ? 30 : 32}
        rx={8}
        fill={tile}
        stroke={stroke}
        strokeWidth={variant === "mono" ? 2 : 0}
      />
      <path
        fill={letter}
        d="M10 9h3.4v5.1l4.2-5.1h4l-5.4 6.5 5.8 7h-4.1l-4.5-5.5V23H10V9z"
      />
    </svg>
  );
}
