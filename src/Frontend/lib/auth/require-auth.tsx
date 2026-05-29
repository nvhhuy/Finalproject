"use client";

import { useAuth } from "./auth-context";
import { usePathname, useRouter } from "next/navigation";
import { useEffect } from "react";
import { AppShellSkeleton } from "@/components/ui/skeleton";

export function RequireAuth({ children }: { children: React.ReactNode }) {
  const auth = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  useEffect(() => {
    if (!auth.hydrated) return;
    if (!auth.isAuthenticated) {
      router.replace(`/login?next=${encodeURIComponent(pathname ?? "/app")}`);
    }
  }, [auth.hydrated, auth.isAuthenticated, pathname, router]);

  if (!auth.hydrated) {
    return <AppShellSkeleton />;
  }
  if (!auth.isAuthenticated) return null;
  return children;
}
