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
        "shrink-0 font-semibold tracking-tight max-w-[min(100%,12rem)] truncate sm:max-w-none",
        className,
      )}
      title={APP_NAME}
    >
      {APP_NAME}
    </Link>
  );
}
