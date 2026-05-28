"use client";

import dynamic from "next/dynamic";
import { AppShellSkeleton, LandingSkeleton, Skeleton } from "@/components/ui/skeleton";

/** Google OAuth SDK — only needed on login. */
export const LazyGoogleSignInButton = dynamic(
  () => import("@/components/auth/GoogleSignInButton").then((m) => ({ default: m.GoogleSignInButton })),
  {
    ssr: false,
    loading: () => <Skeleton className="h-10 w-full rounded-lg" />,
  },
);

/** QR renderer — only needed during 2FA setup. */
export const LazyQrCode = dynamic(
  () => import("@/components/auth/QrCode").then((m) => ({ default: m.QrCode })),
  {
    ssr: false,
    loading: () => <Skeleton className="h-[196px] w-[196px] rounded-xl" />,
  },
);

/** Authenticated chrome — split from initial public/auth bundles. */
export const LazyAppShell = dynamic(
  () => import("@/components/layout/AppShell").then((m) => ({ default: m.AppShell })),
  { loading: () => <AppShellSkeleton /> },
);

/** Marketing landing — not needed for /app or auth routes. */
export const LazyPublicShell = dynamic(
  () => import("@/components/layout/PublicShell").then((m) => ({ default: m.PublicShell })),
  { loading: () => <LandingSkeleton /> },
);

/** Dev-only; keep out of production bundle. */
export const LazyReactQueryDevtools = dynamic(
  () =>
    import("@tanstack/react-query-devtools").then((m) => ({
      default: m.ReactQueryDevtools,
    })),
  { ssr: false },
);
