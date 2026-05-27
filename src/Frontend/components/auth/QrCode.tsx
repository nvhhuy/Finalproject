"use client";

import QRCodeSVG from "react-qr-code";

/** Renders a QR for otpauth:// URIs (2FA setup). SVG-based — no canvas, works with Next/Turbopack. */
export function QrCode({ value, size = 180 }: { value: string; size?: number }) {
  if (!value) return null;

  return (
    <div className="inline-block rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] p-3 shadow-sm">
      <QRCodeSVG value={value} size={size} level="M" />
    </div>
  );
}
