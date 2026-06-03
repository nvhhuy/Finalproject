"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/** @deprecated Use /app/account?tab=preferences */
export default function SettingsRedirectPage() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/app/account?tab=preferences");
  }, [router]);
  return null;
}
