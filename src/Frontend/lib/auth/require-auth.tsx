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
    if (!auth.tokens?.accessToken) {
      router.replace(`/login?next=${encodeURIComponent(pathname ?? "/app")}`);
      return;
    }
    if (!auth.isAuthenticated) {
      const email = auth.user?.email ?? "";
      const q = email ? `?email=${encodeURIComponent(email)}` : "";
      router.replace(`/awaiting-verification${q}`);
    }
  }, [auth.hydrated, auth.isAuthenticated, auth.tokens?.accessToken, auth.user?.email, pathname, router]);

  if (!auth.hydrated) {
    return <AppShellSkeleton />;
  }
  if (!auth.isAuthenticated) return null;
  return children;
}
