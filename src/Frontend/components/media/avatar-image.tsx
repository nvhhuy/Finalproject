"use client";

import { useEffect, useState } from "react";
import { getApiBaseUrl } from "@/lib/api/config";
import { loadTokens } from "@/lib/auth/tokens";
import { cn } from "@/lib/ui/cn";

type AvatarImageProps = {
  mediaAssetId?: string | null;
  /** Legacy presigned URL fallback when content endpoint is unavailable. */
  fallbackUrl?: string | null;
  cacheKey?: string | number;
  alt?: string;
  className?: string;
};

export function AvatarImage({
  mediaAssetId,
  fallbackUrl,
  cacheKey,
  alt = "",
  className,
}: AvatarImageProps) {
  const [src, setSrc] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let revoked: string | null = null;
    let cancelled = false;
    setFailed(false);

    async function load() {
      if (!mediaAssetId) {
        if (!cancelled) {
          setSrc(
            fallbackUrl
              ? `${fallbackUrl}${fallbackUrl.includes("?") ? "&" : "?"}v=${cacheKey ?? 0}`
              : null,
          );
          setFailed(!fallbackUrl);
        }
        return;
      }

      const token = loadTokens()?.accessToken;
      if (!token) {
        if (!cancelled) {
          setSrc(null);
          setFailed(true);
        }
        return;
      }

      const base = getApiBaseUrl().replace(/\/$/, "");
      const path = `/api/v1/media/${mediaAssetId}/content`;
      const url = base ? `${base}${path}` : path;

      try {
        const res = await fetch(url, {
          headers: { Authorization: `Bearer ${token}` },
          cache: "no-store",
        });
        if (!res.ok) throw new Error("avatar fetch failed");
        const blob = await res.blob();
        if (cancelled) return;
        revoked = URL.createObjectURL(blob);
        setSrc(revoked);
        setFailed(false);
      } catch {
        if (cancelled) return;
        if (fallbackUrl) {
          setSrc(
            `${fallbackUrl}${fallbackUrl.includes("?") ? "&" : "?"}v=${cacheKey ?? 0}`,
          );
          setFailed(false);
        } else {
          setSrc(null);
          setFailed(true);
        }
      }
    }

    void load();

    return () => {
      cancelled = true;
      if (revoked) URL.revokeObjectURL(revoked);
    };
  }, [mediaAssetId, fallbackUrl, cacheKey]);

  if (failed || !src) return null;

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} className={cn(className)} />
  );
}
