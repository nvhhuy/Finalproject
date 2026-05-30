"use client";

import { Turnstile } from "@marsidev/react-turnstile";

type TurnstileWidgetProps = {
  onVerify: (token: string) => void;
  onExpire?: () => void;
  onError?: () => void;
  className?: string;
};

const SITE_KEY = process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY ?? "";

/**
 * Renders the Cloudflare Turnstile challenge widget.
 * When NEXT_PUBLIC_TURNSTILE_SITE_KEY is not set the widget is not rendered
 * (development without Turnstile configured will still work because the backend
 * also has Turnstile.Enabled=false by default).
 */
export function TurnstileWidget({ onVerify, onExpire, onError, className }: TurnstileWidgetProps) {
  if (!SITE_KEY) return null;

  return (
    <Turnstile
      siteKey={SITE_KEY}
      onSuccess={onVerify}
      onExpire={onExpire}
      onError={onError}
      className={className}
      options={{ theme: "auto" }}
    />
  );
}
