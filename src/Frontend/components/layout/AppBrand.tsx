import Link from "next/link";
import { APP_NAME } from "@/lib/config/branding";
import { cn } from "@/lib/ui/cn";

export function AppBrand({
  href = "/",
  className,
}: {
  href?: string;
  className?: string;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "max-w-[min(100%,12rem)] truncate font-semibold tracking-tight sm:max-w-none",
        className,
      )}
      title={APP_NAME}
    >
      {APP_NAME}
    </Link>
  );
}
