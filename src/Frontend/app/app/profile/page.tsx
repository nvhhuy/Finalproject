"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";

/** @deprecated Use /app/account?tab=profile */
export default function ProfileRedirectPage() {
  const router = useRouter();
  useEffect(() => {
    router.replace("/app/account?tab=profile");
  }, [router]);
  return null;
}
