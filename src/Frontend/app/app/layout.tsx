"use client";

import { LazyAppShell } from "@/lib/ui/lazy-components";
import { RequireAuth } from "@/lib/auth/require-auth";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <RequireAuth>
      <LazyAppShell>{children}</LazyAppShell>
    </RequireAuth>
  );
}
