"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/** @deprecated Use /app/account?tab=security */
export default function TwoFactorRedirectPage() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/app/account?tab=security");
  }, [router]);
  return null;
}
