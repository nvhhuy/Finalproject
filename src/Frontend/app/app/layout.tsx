"use client";

import { AppShell } from "@/components/layout/AppShell";
import { RequireAuth } from "@/lib/auth/require-auth";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <RequireAuth>
      <AppShell>{children}</AppShell>
    </RequireAuth>
  );
}
