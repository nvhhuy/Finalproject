"use client";

import { useAuth } from "./auth-context";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const auth = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const { t } = useTranslation();

  useEffect(() => {
    if (!auth.hydrated) return;
    if (!auth.isAuthenticated) {
      router.replace(`/login?next=${encodeURIComponent(pathname ?? "/app")}`);
    }
  }, [auth.hydrated, auth.isAuthenticated, pathname, router]);

  if (!auth.hydrated) {
    return <div className="p-8 text-sm text-zinc-600">{t("common.loading")}</div>;
  }
  if (!auth.isAuthenticated) return null;
  return children;
}
