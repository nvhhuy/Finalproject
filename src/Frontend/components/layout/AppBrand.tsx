import Link from "next/link";
import { KlassiLogoMark, type KlassiLogoVariant } from "@/components/brand/KlassiLogoMark";
import { APP_NAME } from "@/lib/config/branding";
import { cn } from "@/lib/ui/cn";

export function AppBrand({
  href = "/",
  className,
  logoVariant = "brand",
  showWordmark = true,
}: {
  href?: string;
  className?: string;
  logoVariant?: KlassiLogoVariant;
  showWordmark?: boolean;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "inline-flex min-w-0 max-w-full items-center gap-2 font-semibold tracking-tight",
        className,
      )}
      title={APP_NAME}
    >
      <KlassiLogoMark size={28} variant={logoVariant} />
      {showWordmark ? (
        <span className="truncate">{APP_NAME}</span>
      ) : (
        <span className="sr-only">{APP_NAME}</span>
      )}
    </Link>
  );
}
